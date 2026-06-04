namespace Bezalu.NinjaOne.MCP.Auth.OAuth;

/// <summary>
/// A dynamically-registered OAuth client (RFC 7591). Persisted locally.
/// </summary>
internal sealed record RegisteredClient
{
    public required string ClientId { get; init; }

    /// <summary>Null for public clients (PKCE-only). Present for confidential clients.</summary>
    public string? ClientSecret { get; init; }

    public required IReadOnlyList<string> RedirectUris { get; init; }

    public string? ClientName { get; init; }

    public IReadOnlyList<string> GrantTypes { get; init; } = ["authorization_code", "refresh_token"];

    public IReadOnlyList<string> ResponseTypes { get; init; } = ["code"];

    public string TokenEndpointAuthMethod { get; init; } = "none";

    public long CreatedAt { get; init; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
}

/// <summary>
/// Transient state for an in-flight authorization request, bridged through NinjaOne.
/// Keyed by the <see cref="State"/> value sent to NinjaOne.
/// </summary>
internal sealed record AuthSession
{
    public required string State { get; init; }

    public required string ClientId { get; init; }

    public required string ClientRedirectUri { get; init; }

    /// <summary>The downstream MCP client's state, echoed back on completion.</summary>
    public string? ClientState { get; init; }

    /// <summary>PKCE challenge supplied by the MCP client (required).</summary>
    public required string CodeChallenge { get; init; }

    public string CodeChallengeMethod { get; init; } = "S256";

    public required string Scope { get; init; }

    public long CreatedAt { get; init; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
}

/// <summary>
/// An issued authorization code that maps to a captured NinjaOne token set.
/// Single-use; consumed at the token endpoint.
/// </summary>
internal sealed record AuthorizationCode
{
    public required string Code { get; init; }

    public required string ClientId { get; init; }

    public required string RedirectUri { get; init; }

    public required string CodeChallenge { get; init; }

    public string CodeChallengeMethod { get; init; } = "S256";

    public required string Scope { get; init; }

    public required string NinjaAccessToken { get; init; }

    public string? NinjaRefreshToken { get; init; }

    public required long NinjaAccessTokenExpiresAt { get; init; }

    public long CreatedAt { get; init; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
}

/// <summary>
/// A token record issued by this server. The opaque access/refresh tokens map to the
/// underlying NinjaOne token set, enabling per-user calls and transparent refresh.
/// </summary>
internal sealed record TokenRecord
{
    public required string AccessToken { get; init; }

    public string? RefreshToken { get; init; }

    public required string ClientId { get; init; }

    public required string Scope { get; init; }

    /// <summary>Upstream NinjaOne access token used for API calls.</summary>
    public required string NinjaAccessToken { get; init; }

    public string? NinjaRefreshToken { get; init; }

    /// <summary>Unix seconds when the NinjaOne access token expires.</summary>
    public required long NinjaAccessTokenExpiresAt { get; init; }

    /// <summary>Unix seconds when this server's access token expires.</summary>
    public required long AccessTokenExpiresAt { get; init; }
}
