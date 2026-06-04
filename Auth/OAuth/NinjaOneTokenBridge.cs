using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bezalu.NinjaOne.MCP.Auth.OAuth;

/// <summary>
/// Bridges the embedded authorization server to the upstream NinjaOne OAuth server using
/// a single shared NinjaOne API application credential. All resulting tokens remain
/// user-scoped (issued via the authorization code flow against the signed-in user).
/// </summary>
internal sealed class NinjaOneTokenBridge(IHttpClientFactory httpClientFactory, OAuthServerOptions options)
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly OAuthServerOptions _options = options;

    /// <summary>
    /// Builds the upstream NinjaOne authorization URL the user's browser is redirected to.
    /// </summary>
    public string BuildAuthorizeUrl(string redirectUri, string state)
    {
        var query = new Dictionary<string, string?>
        {
            ["response_type"] = "code",
            ["client_id"] = _options.NinjaClientId,
            ["redirect_uri"] = redirectUri,
            ["scope"] = _options.Scopes,
            ["state"] = state,
        };

        var encoded = string.Join('&', query
            .Where(kvp => kvp.Value is not null)
            .Select(kvp => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value!)}"));

        return $"{_options.NinjaAuthorizeEndpoint}?{encoded}";
    }

    /// <summary>Exchanges a NinjaOne authorization code for a token set.</summary>
    public Task<NinjaTokenResponse> ExchangeCodeAsync(string code, string redirectUri, CancellationToken cancellationToken) =>
        PostTokenAsync(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
        }, cancellationToken);

    /// <summary>Refreshes a NinjaOne token set using a refresh token.</summary>
    public Task<NinjaTokenResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken) =>
        PostTokenAsync(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
        }, cancellationToken);

    private async Task<NinjaTokenResponse> PostTokenAsync(Dictionary<string, string> form, CancellationToken cancellationToken)
    {
        form["client_id"] = _options.NinjaClientId;
        form["client_secret"] = _options.NinjaClientSecret;

        using var request = new HttpRequestMessage(HttpMethod.Post, _options.NinjaTokenEndpoint)
        {
            Content = new FormUrlEncodedContent(form),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var httpClient = _httpClientFactory.CreateClient("NinjaOne");
        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            using var error = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            var description = error.RootElement.TryGetProperty("error_description", out var d) ? d.GetString()
                : error.RootElement.TryGetProperty("error", out var e) ? e.GetString()
                : response.ReasonPhrase;
            throw new NinjaOneTokenException(description ?? "NinjaOne token request failed");
        }

        var token = await JsonSerializer.DeserializeAsync<NinjaTokenResponse>(stream, cancellationToken: cancellationToken).ConfigureAwait(false)
            ?? throw new NinjaOneTokenException("Empty token response from NinjaOne");
        return token;
    }
}

/// <summary>Raised when an upstream NinjaOne token request fails.</summary>
internal sealed class NinjaOneTokenException(string message) : Exception(message);

/// <summary>Subset of the NinjaOne OAuth token response.</summary>
internal sealed record NinjaTokenResponse
{
    [JsonPropertyName("access_token")]
    public required string AccessToken { get; init; }

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; init; }

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; init; }

    [JsonPropertyName("scope")]
    public string? Scope { get; init; }

    /// <summary>Computes the absolute expiry (unix seconds) from the relative lifetime.</summary>
    public long ExpiresAt => DateTimeOffset.UtcNow.ToUnixTimeSeconds() + ExpiresIn;
}
