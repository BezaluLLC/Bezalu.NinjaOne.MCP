using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Bezalu.NinjaOne.MCP.Auth.OAuth;

/// <summary>
/// Maps the embedded OAuth 2.0 authorization server endpoints required for MCP spec compliance:
/// authorization-server metadata, RFC 7591 Dynamic Client Registration, authorize, callback, and token.
/// </summary>
internal static class OAuthEndpoints
{
    /// <summary>The access-token lifetime issued by this server (1 hour).</summary>
    private const int AccessTokenLifetimeSeconds = 3600;

    /// <summary>Token-endpoint authentication methods this server supports and advertises.</summary>
    private static readonly string[] SupportedAuthMethods = ["none", "client_secret_post"];

    /// <summary>The relative path NinjaOne redirects back to after user authorization.</summary>
    public const string CallbackPath = "/callback";

    public static IEndpointRouteBuilder MapOAuthServer(this IEndpointRouteBuilder app)
    {
        app.MapGet("/.well-known/oauth-authorization-server", GetMetadata).AllowAnonymous();
        app.MapPost("/register", RegisterClientAsync).AllowAnonymous();
        app.MapGet("/authorize", AuthorizeAsync).AllowAnonymous();
        app.MapGet(CallbackPath, CallbackAsync).AllowAnonymous();
        app.MapPost("/token", TokenAsync).AllowAnonymous();
        return app;
    }

    // --- Authorization-server metadata (RFC 8414) --------------------------

    private static IResult GetMetadata(HttpContext context)
    {
        var issuer = GetIssuer(context);
        var metadata = new Dictionary<string, object?>
        {
            ["issuer"] = issuer,
            ["authorization_endpoint"] = $"{issuer}/authorize",
            ["token_endpoint"] = $"{issuer}/token",
            ["registration_endpoint"] = $"{issuer}/register",
            ["response_types_supported"] = new[] { "code" },
            ["grant_types_supported"] = new[] { "authorization_code", "refresh_token" },
            ["code_challenge_methods_supported"] = new[] { Pkce.S256Method },
            ["token_endpoint_auth_methods_supported"] = SupportedAuthMethods,
            ["scopes_supported"] = context.RequestServices.GetRequiredService<OAuthServerOptions>()
                .Scopes.Split(' ', StringSplitOptions.RemoveEmptyEntries),
        };
        return Results.Json(metadata);
    }

    // --- Dynamic Client Registration (RFC 7591) ----------------------------

    private static async Task<IResult> RegisterClientAsync(
        HttpContext context,
        FileOAuthStore store,
        [FromBody] ClientRegistrationRequest request,
        CancellationToken cancellationToken)
    {
        if (request.RedirectUris is null || request.RedirectUris.Count == 0)
            return InvalidClientMetadata("redirect_uris is required");

        foreach (var uri in request.RedirectUris)
        {
            if (!Uri.TryCreate(uri, UriKind.Absolute, out _))
                return InvalidClientMetadata($"invalid redirect_uri '{uri}'");
        }

        var authMethod = string.IsNullOrWhiteSpace(request.TokenEndpointAuthMethod)
            ? "none"
            : request.TokenEndpointAuthMethod;
        if (!SupportedAuthMethods.Contains(authMethod))
            return InvalidClientMetadata($"unsupported token_endpoint_auth_method '{authMethod}'");
        var isConfidential = !string.Equals(authMethod, "none", StringComparison.OrdinalIgnoreCase);

        var client = new RegisteredClient
        {
            ClientId = Pkce.NewToken(16),
            ClientSecret = isConfidential ? Pkce.NewToken(32) : null,
            RedirectUris = request.RedirectUris,
            ClientName = request.ClientName,
            GrantTypes = request.GrantTypes is { Count: > 0 } ? request.GrantTypes : ["authorization_code", "refresh_token"],
            ResponseTypes = request.ResponseTypes is { Count: > 0 } ? request.ResponseTypes : ["code"],
            TokenEndpointAuthMethod = authMethod,
        };

        await store.SaveClientAsync(client, cancellationToken).ConfigureAwait(false);

        var response = new Dictionary<string, object?>
        {
            ["client_id"] = client.ClientId,
            ["redirect_uris"] = client.RedirectUris,
            ["grant_types"] = client.GrantTypes,
            ["response_types"] = client.ResponseTypes,
            ["token_endpoint_auth_method"] = client.TokenEndpointAuthMethod,
            ["client_id_issued_at"] = client.CreatedAt,
        };
        if (client.ClientSecret is not null)
        {
            response["client_secret"] = client.ClientSecret;
            response["client_secret_expires_at"] = 0; // never expires
        }
        if (client.ClientName is not null)
            response["client_name"] = client.ClientName;

        return Results.Json(response, statusCode: StatusCodes.Status201Created);
    }

    // --- Authorization endpoint --------------------------------------------

    private static IResult AuthorizeAsync(
        HttpContext context,
        FileOAuthStore store,
        NinjaOneTokenBridge bridge,
        OAuthServerOptions options,
        [FromQuery(Name = "response_type")] string? responseType,
        [FromQuery(Name = "client_id")] string? clientId,
        [FromQuery(Name = "redirect_uri")] string? redirectUri,
        [FromQuery(Name = "code_challenge")] string? codeChallenge,
        [FromQuery(Name = "code_challenge_method")] string? codeChallengeMethod,
        [FromQuery] string? state)
    {
        if (!string.Equals(responseType, "code", StringComparison.Ordinal))
            return AuthorizeError(redirectUri, state, "unsupported_response_type", "only response_type=code is supported");
        if (string.IsNullOrWhiteSpace(clientId))
            return Results.BadRequest(new { error = "invalid_request", error_description = "client_id is required" });
        if (string.IsNullOrWhiteSpace(codeChallenge))
            return AuthorizeError(redirectUri, state, "invalid_request", "code_challenge is required (PKCE)");

        // Only S256 is advertised/supported; an explicitly-supplied other method is rejected.
        var challengeMethod = string.IsNullOrWhiteSpace(codeChallengeMethod) ? Pkce.S256Method : codeChallengeMethod;
        if (!Pkce.IsSupportedMethod(challengeMethod))
            return AuthorizeError(redirectUri, state, "invalid_request", "only S256 code_challenge_method is supported");

        var client = store.GetClient(clientId);
        if (client is null)
            return Results.BadRequest(new { error = "invalid_client", error_description = "unknown client_id" });

        if (string.IsNullOrWhiteSpace(redirectUri) || !client.RedirectUris.Contains(redirectUri))
            return Results.BadRequest(new { error = "invalid_request", error_description = "redirect_uri not registered for client" });

        // Bridge state ties the NinjaOne callback back to this MCP authorization request.
        var bridgeState = Pkce.NewToken(24);
        store.SaveSession(new AuthSession
        {
            State = bridgeState,
            ClientId = clientId,
            ClientRedirectUri = redirectUri,
            ClientState = state,
            CodeChallenge = codeChallenge,
            CodeChallengeMethod = challengeMethod,
            // The upstream NinjaOne request always uses the configured scopes, so record those
            // (not the caller-supplied value) to keep the issued token's scope accurate.
            Scope = options.Scopes,
        });

        var ninjaRedirectUri = $"{GetIssuer(context)}{CallbackPath}";
        var ninjaAuthorizeUrl = bridge.BuildAuthorizeUrl(ninjaRedirectUri, bridgeState);
        return Results.Redirect(ninjaAuthorizeUrl);
    }

    // --- NinjaOne callback -------------------------------------------------

    private static async Task<IResult> CallbackAsync(
        HttpContext context,
        FileOAuthStore store,
        NinjaOneTokenBridge bridge,
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery] string? error,
        [FromQuery(Name = "error_description")] string? errorDescription,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(state))
            return Results.BadRequest(new { error = "invalid_request", error_description = "missing state" });

        var session = store.TakeSession(state);
        if (session is null)
            return Results.BadRequest(new { error = "invalid_request", error_description = "unknown or expired state" });

        if (!string.IsNullOrWhiteSpace(error))
            return RedirectWithError(session.ClientRedirectUri, session.ClientState, error, errorDescription);

        if (string.IsNullOrWhiteSpace(code))
            return RedirectWithError(session.ClientRedirectUri, session.ClientState, "invalid_request", "missing authorization code");

        NinjaTokenResponse ninjaToken;
        try
        {
            var ninjaRedirectUri = $"{GetIssuer(context)}{CallbackPath}";
            ninjaToken = await bridge.ExchangeCodeAsync(code, ninjaRedirectUri, cancellationToken).ConfigureAwait(false);
        }
        catch (NinjaOneTokenException ex)
        {
            return RedirectWithError(session.ClientRedirectUri, session.ClientState, "server_error", ex.Message);
        }

        var authCode = new AuthorizationCode
        {
            Code = Pkce.NewToken(32),
            ClientId = session.ClientId,
            RedirectUri = session.ClientRedirectUri,
            CodeChallenge = session.CodeChallenge,
            CodeChallengeMethod = session.CodeChallengeMethod,
            Scope = session.Scope,
            NinjaAccessToken = ninjaToken.AccessToken,
            NinjaRefreshToken = ninjaToken.RefreshToken,
            NinjaAccessTokenExpiresAt = ninjaToken.ExpiresAt,
        };
        store.SaveCode(authCode);

        var separator = session.ClientRedirectUri.Contains('?') ? '&' : '?';
        var query = $"code={Uri.EscapeDataString(authCode.Code)}";
        if (!string.IsNullOrWhiteSpace(session.ClientState))
            query += $"&state={Uri.EscapeDataString(session.ClientState)}";
        return Results.Redirect($"{session.ClientRedirectUri}{separator}{query}");
    }

    // --- Token endpoint ----------------------------------------------------

    private static async Task<IResult> TokenAsync(
        HttpContext context,
        FileOAuthStore store,
        NinjaOneTokenBridge bridge,
        CancellationToken cancellationToken)
    {
        var form = await context.Request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
        var grantType = form["grant_type"].ToString();

        return grantType switch
        {
            "authorization_code" => await ExchangeAuthorizationCodeAsync(store, form, cancellationToken).ConfigureAwait(false),
            "refresh_token" => await ExchangeRefreshTokenAsync(store, bridge, form, cancellationToken).ConfigureAwait(false),
            _ => Results.BadRequest(new { error = "unsupported_grant_type", error_description = $"grant_type '{grantType}' is not supported" }),
        };
    }

    /// <summary>
    /// Authenticates the client per its registered <c>token_endpoint_auth_method</c>. Confidential
    /// clients (issued a secret) must present a matching <c>client_secret</c> via client_secret_post.
    /// Returns an error result when authentication fails, otherwise null.
    /// </summary>
    private static IResult? AuthenticateClient(RegisteredClient client, IFormCollection form)
    {
        if (client.ClientSecret is null)
            return null;

        var presented = form["client_secret"].ToString();
        if (string.IsNullOrEmpty(presented) ||
            !CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(presented),
                System.Text.Encoding.UTF8.GetBytes(client.ClientSecret)))
        {
            return Results.Json(
                new { error = "invalid_client", error_description = "client authentication failed" },
                statusCode: StatusCodes.Status401Unauthorized);
        }

        return null;
    }

    private static async Task<IResult> ExchangeAuthorizationCodeAsync(
        FileOAuthStore store,
        IFormCollection form,
        CancellationToken cancellationToken)
    {
        var code = form["code"].ToString();
        var clientId = form["client_id"].ToString();
        var redirectUri = form["redirect_uri"].ToString();
        var codeVerifier = form["code_verifier"].ToString();

        if (string.IsNullOrWhiteSpace(code))
            return Results.BadRequest(new { error = "invalid_request", error_description = "code is required" });

        var authCode = store.TakeCode(code);
        if (authCode is null)
            return Results.BadRequest(new { error = "invalid_grant", error_description = "invalid or expired authorization code" });

        if (!string.Equals(authCode.ClientId, clientId, StringComparison.Ordinal))
            return Results.BadRequest(new { error = "invalid_grant", error_description = "client_id mismatch" });

        var client = store.GetClient(authCode.ClientId);
        if (client is null)
            return Results.BadRequest(new { error = "invalid_grant", error_description = "unknown client" });
        if (AuthenticateClient(client, form) is { } authError)
            return authError;

        if (!string.Equals(authCode.RedirectUri, redirectUri, StringComparison.Ordinal))
            return Results.BadRequest(new { error = "invalid_grant", error_description = "redirect_uri mismatch" });

        if (string.IsNullOrWhiteSpace(codeVerifier) || !Pkce.Verify(codeVerifier, authCode.CodeChallenge, authCode.CodeChallengeMethod))
            return Results.BadRequest(new { error = "invalid_grant", error_description = "PKCE verification failed" });

        var token = await IssueTokenAsync(store, authCode.ClientId, authCode.Scope,
            authCode.NinjaAccessToken, authCode.NinjaRefreshToken, authCode.NinjaAccessTokenExpiresAt, cancellationToken)
            .ConfigureAwait(false);

        return TokenResponse(token);
    }

    private static async Task<IResult> ExchangeRefreshTokenAsync(
        FileOAuthStore store,
        NinjaOneTokenBridge bridge,
        IFormCollection form,
        CancellationToken cancellationToken)
    {
        var refreshToken = form["refresh_token"].ToString();
        if (string.IsNullOrWhiteSpace(refreshToken))
            return Results.BadRequest(new { error = "invalid_request", error_description = "refresh_token is required" });

        var existing = store.GetByRefreshToken(refreshToken);
        if (existing is null)
            return Results.BadRequest(new { error = "invalid_grant", error_description = "invalid refresh_token" });

        var client = store.GetClient(existing.ClientId);
        if (client is null)
            return Results.BadRequest(new { error = "invalid_grant", error_description = "unknown client" });
        if (AuthenticateClient(client, form) is { } authError)
            return authError;

        string ninjaAccessToken = existing.NinjaAccessToken;
        string? ninjaRefreshToken = existing.NinjaRefreshToken;
        long ninjaExpiresAt = existing.NinjaAccessTokenExpiresAt;

        // Refresh the upstream NinjaOne token if we can, so the user session stays alive.
        if (!string.IsNullOrWhiteSpace(existing.NinjaRefreshToken))
        {
            try
            {
                var refreshed = await bridge.RefreshAsync(existing.NinjaRefreshToken, cancellationToken).ConfigureAwait(false);
                ninjaAccessToken = refreshed.AccessToken;
                ninjaRefreshToken = refreshed.RefreshToken ?? existing.NinjaRefreshToken;
                ninjaExpiresAt = refreshed.ExpiresAt;
            }
            catch (NinjaOneTokenException ex)
            {
                return Results.BadRequest(new { error = "invalid_grant", error_description = ex.Message });
            }
        }

        var newToken = BuildTokenRecord(existing.ClientId, existing.Scope, ninjaAccessToken, ninjaRefreshToken, ninjaExpiresAt);
        await store.ReplaceTokenAsync(existing.AccessToken, newToken, cancellationToken).ConfigureAwait(false);
        return TokenResponse(newToken);
    }

    private static async Task<TokenRecord> IssueTokenAsync(
        FileOAuthStore store,
        string clientId,
        string scope,
        string ninjaAccessToken,
        string? ninjaRefreshToken,
        long ninjaExpiresAt,
        CancellationToken cancellationToken)
    {
        var token = BuildTokenRecord(clientId, scope, ninjaAccessToken, ninjaRefreshToken, ninjaExpiresAt);
        await store.SaveTokenAsync(token, cancellationToken).ConfigureAwait(false);
        return token;
    }

    private static TokenRecord BuildTokenRecord(
        string clientId,
        string scope,
        string ninjaAccessToken,
        string? ninjaRefreshToken,
        long ninjaExpiresAt) => new()
        {
            AccessToken = Pkce.NewToken(32),
            // Only issue a refresh token when the upstream NinjaOne flow actually provided one;
            // otherwise we cannot renew upstream credentials and a refresh would later fail.
            RefreshToken = string.IsNullOrEmpty(ninjaRefreshToken) ? null : Pkce.NewToken(32),
            ClientId = clientId,
            Scope = scope,
            NinjaAccessToken = ninjaAccessToken,
            NinjaRefreshToken = ninjaRefreshToken,
            NinjaAccessTokenExpiresAt = ninjaExpiresAt,
            AccessTokenExpiresAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + AccessTokenLifetimeSeconds,
        };

    private static IResult TokenResponse(TokenRecord token)
    {
        var response = new Dictionary<string, object?>
        {
            ["access_token"] = token.AccessToken,
            ["token_type"] = "Bearer",
            ["expires_in"] = AccessTokenLifetimeSeconds,
            ["scope"] = token.Scope,
        };
        if (!string.IsNullOrEmpty(token.RefreshToken))
            response["refresh_token"] = token.RefreshToken;
        return Results.Json(response);
    }

    // --- Helpers -----------------------------------------------------------

    private static IResult InvalidClientMetadata(string description) =>
        Results.BadRequest(new { error = "invalid_client_metadata", error_description = description });

    private static IResult AuthorizeError(string? redirectUri, string? state, string error, string description)
    {
        if (string.IsNullOrWhiteSpace(redirectUri) || !Uri.TryCreate(redirectUri, UriKind.Absolute, out _))
            return Results.BadRequest(new { error, error_description = description });
        return RedirectWithError(redirectUri, state, error, description);
    }

    private static IResult RedirectWithError(string redirectUri, string? state, string error, string? description)
    {
        var separator = redirectUri.Contains('?') ? '&' : '?';
        var query = $"error={Uri.EscapeDataString(error)}";
        if (!string.IsNullOrWhiteSpace(description))
            query += $"&error_description={Uri.EscapeDataString(description)}";
        if (!string.IsNullOrWhiteSpace(state))
            query += $"&state={Uri.EscapeDataString(state)}";
        return Results.Redirect($"{redirectUri}{separator}{query}");
    }

    /// <summary>
    /// Resolves the externally-visible issuer URL, honoring an explicit override or
    /// forwarded headers when running behind a reverse proxy.
    /// </summary>
    internal static string GetIssuer(HttpContext context)
    {
        var options = context.RequestServices.GetRequiredService<OAuthServerOptions>();
        if (!string.IsNullOrWhiteSpace(options.PublicBaseUrl))
            return options.PublicBaseUrl.TrimEnd('/');

        var request = context.Request;
        return $"{request.Scheme}://{request.Host}{request.PathBase}".TrimEnd('/');
    }
}

/// <summary>RFC 7591 client registration request body.</summary>
internal sealed record ClientRegistrationRequest
{
    [JsonPropertyName("redirect_uris")]
    public IReadOnlyList<string>? RedirectUris { get; init; }

    [JsonPropertyName("client_name")]
    public string? ClientName { get; init; }

    [JsonPropertyName("grant_types")]
    public IReadOnlyList<string>? GrantTypes { get; init; }

    [JsonPropertyName("response_types")]
    public IReadOnlyList<string>? ResponseTypes { get; init; }

    [JsonPropertyName("token_endpoint_auth_method")]
    public string? TokenEndpointAuthMethod { get; init; }
}
