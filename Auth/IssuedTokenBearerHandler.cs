using System.Security.Claims;
using System.Text.Encodings.Web;
using Bezalu.NinjaOne.MCP.Auth.OAuth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Bezalu.NinjaOne.MCP.Auth;

/// <summary>
/// Validates bearer tokens that were issued by this server's embedded authorization server.
/// On success, resolves (and transparently refreshes) the underlying NinjaOne access token and
/// stashes it in <see cref="HttpContext.Items"/> so downstream NinjaOne calls run as the user.
/// </summary>
internal sealed class IssuedTokenBearerHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    FileOAuthStore store,
    NinjaOneTokenBridge bridge)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    /// <summary>Key used to pass the resolved NinjaOne access token to the client factory.</summary>
    public const string NinjaTokenItemKey = "NinjaOne.AccessToken";

    /// <summary>Refresh the upstream NinjaOne token when it is within this window of expiry.</summary>
    private const int RefreshSkewSeconds = 60;

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authHeader = Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return AuthenticateResult.NoResult();

        var accessToken = authHeader["Bearer ".Length..].Trim();
        if (string.IsNullOrWhiteSpace(accessToken))
            return AuthenticateResult.NoResult();

        var record = store.GetByAccessToken(accessToken);
        if (record is null)
            return AuthenticateResult.Fail("Unknown access token");

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (record.AccessTokenExpiresAt <= now)
            return AuthenticateResult.Fail("Access token expired");

        var ninjaAccessToken = record.NinjaAccessToken;

        // If the upstream NinjaOne token is already expired and cannot be refreshed, fail now so the
        // client reauthorizes instead of receiving a 200 and a guaranteed-to-fail NinjaOne call.
        if (record.NinjaAccessTokenExpiresAt <= now && string.IsNullOrWhiteSpace(record.NinjaRefreshToken))
            return AuthenticateResult.Fail("Upstream NinjaOne token expired and cannot be refreshed");

        // Transparently refresh the upstream NinjaOne token if it is expired or about to expire.
        if (record.NinjaAccessTokenExpiresAt <= now + RefreshSkewSeconds && !string.IsNullOrWhiteSpace(record.NinjaRefreshToken))
        {
            try
            {
                var refreshed = await bridge.RefreshAsync(record.NinjaRefreshToken, Context.RequestAborted).ConfigureAwait(false);
                ninjaAccessToken = refreshed.AccessToken;
                var updated = record with
                {
                    NinjaAccessToken = refreshed.AccessToken,
                    NinjaRefreshToken = refreshed.RefreshToken ?? record.NinjaRefreshToken,
                    NinjaAccessTokenExpiresAt = refreshed.ExpiresAt,
                };
                await store.SaveTokenAsync(updated, Context.RequestAborted).ConfigureAwait(false);
            }
            catch (NinjaOneTokenException ex)
            {
                Logger.LogWarning(ex, "Failed to refresh NinjaOne token");
                return AuthenticateResult.Fail("Failed to refresh upstream NinjaOne token");
            }
        }

        Context.Items[NinjaTokenItemKey] = ninjaAccessToken;

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, record.ClientId),
            new Claim("scope", record.Scope),
        };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return AuthenticateResult.Success(ticket);
    }
}
