namespace Bezalu.NinjaOne.MCP.Auth.OAuth;

/// <summary>
/// Configuration for the MCP server's embedded OAuth 2.0 authorization server and the
/// upstream NinjaOne authorization server it bridges to.
/// </summary>
internal sealed class OAuthServerOptions
{
    /// <summary>NinjaOne instance base URL (e.g., https://app.ninjarmm.com).</summary>
    public required string InstanceUrl { get; init; }

    /// <summary>The single NinjaOne API application client id used for all user flows.</summary>
    public required string NinjaClientId { get; init; }

    /// <summary>The single NinjaOne API application client secret used for all user flows.</summary>
    public required string NinjaClientSecret { get; init; }

    /// <summary>Space-delimited NinjaOne scopes requested during the upstream authorization flow.</summary>
    public required string Scopes { get; init; }

    /// <summary>
    /// Absolute or relative path to the directory used for local persistence of registered
    /// clients and issued tokens. Defaults to <c>/app/data</c>. Authorization codes and in-flight
    /// authorization sessions are kept in memory only and are never written here.
    /// </summary>
    public required string DataPath { get; init; }

    /// <summary>
    /// Optional explicit public base URL for this server (e.g., https://mcp.example.com).
    /// When unset, the issuer is derived from the incoming request (honoring forwarded headers).
    /// </summary>
    public string? PublicBaseUrl { get; init; }

    /// <summary>
    /// When true, X-Forwarded-* headers are trusted from any source. Only enable this when the
    /// server is exclusively reachable through a trusted reverse proxy that sets these headers,
    /// otherwise a direct client could spoof the host/scheme used to build issuer/redirect URLs.
    /// </summary>
    public bool TrustForwardedHeaders { get; init; }

    /// <summary>NinjaOne authorization endpoint.</summary>
    public string NinjaAuthorizeEndpoint => $"{TrimmedInstance}/ws/oauth/authorize";

    /// <summary>NinjaOne token endpoint.</summary>
    public string NinjaTokenEndpoint => $"{TrimmedInstance}/ws/oauth/token";

    private string TrimmedInstance => InstanceUrl.TrimEnd('/');

    public static OAuthServerOptions FromConfiguration(IConfiguration configuration)
    {
        var instanceUrl = configuration["NINJAONE_INSTANCE"]
            ?? throw new InvalidOperationException("NINJAONE_INSTANCE environment variable is required (e.g., https://app.ninjarmm.com)");
        var clientId = configuration["NINJAONE_CLIENT_ID"]
            ?? throw new InvalidOperationException("NINJAONE_CLIENT_ID environment variable is required for the OAuth bridge");
        var clientSecret = configuration["NINJAONE_CLIENT_SECRET"]
            ?? throw new InvalidOperationException("NINJAONE_CLIENT_SECRET environment variable is required for the OAuth bridge");

        return new OAuthServerOptions
        {
            InstanceUrl = instanceUrl,
            NinjaClientId = clientId,
            NinjaClientSecret = clientSecret,
            Scopes = configuration["NINJAONE_SCOPES"] ?? "monitoring management offline_access",
            DataPath = configuration["MCP_DATA_PATH"] ?? "/app/data",
            PublicBaseUrl = configuration["PUBLIC_BASE_URL"],
            TrustForwardedHeaders = configuration.GetValue("TRUST_FORWARDED_HEADERS", false),
        };
    }
}
