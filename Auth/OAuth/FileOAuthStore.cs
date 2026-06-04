using System.Collections.Concurrent;
using System.Text.Json;

namespace Bezalu.NinjaOne.MCP.Auth.OAuth;

/// <summary>
/// Thread-safe, file-backed persistence for OAuth clients, in-flight authorization sessions,
/// single-use authorization codes, and issued tokens.
/// </summary>
/// <remarks>
/// All state lives under a single configurable directory (see <see cref="OAuthServerOptions.DataPath"/>),
/// making the container cloud-neutral and portable via a Docker volume mount. Each entity type is stored
/// as a separate JSON document. In-flight sessions and codes are short-lived and kept in memory only.
/// </remarks>
internal sealed class FileOAuthStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>Lifetime for in-flight authorization sessions and single-use codes before eviction.</summary>
    private static readonly TimeSpan TransientLifetime = TimeSpan.FromMinutes(10);

    private readonly string _clientsFile;
    private readonly string _tokensFile;
    private readonly SemaphoreSlim _fileGate = new(1, 1);

    // Guards the in-memory client/token dictionaries for both reads and writes.
    private readonly object _sync = new();

    // Short-lived, in-memory only (do not need to survive restarts).
    private readonly ConcurrentDictionary<string, AuthSession> _sessions = new();
    private readonly ConcurrentDictionary<string, AuthorizationCode> _codes = new();

    private readonly Dictionary<string, RegisteredClient> _clients;
    private readonly Dictionary<string, TokenRecord> _tokens;

    public FileOAuthStore(OAuthServerOptions options)
    {
        var dir = options.DataPath;
        Directory.CreateDirectory(dir);
        _clientsFile = Path.Combine(dir, "clients.json");
        _tokensFile = Path.Combine(dir, "tokens.json");

        _clients = Load<Dictionary<string, RegisteredClient>>(_clientsFile) ?? [];
        _tokens = Load<Dictionary<string, TokenRecord>>(_tokensFile) ?? [];
        PruneExpiredTokens();
    }

    // --- Clients -----------------------------------------------------------

    public async Task SaveClientAsync(RegisteredClient client, CancellationToken cancellationToken = default)
    {
        string snapshot;
        lock (_sync)
        {
            _clients[client.ClientId] = client;
            snapshot = JsonSerializer.Serialize(_clients, JsonOptions);
        }
        await PersistAsync(_clientsFile, snapshot, cancellationToken).ConfigureAwait(false);
    }

    public RegisteredClient? GetClient(string clientId)
    {
        lock (_sync)
            return _clients.TryGetValue(clientId, out var client) ? client : null;
    }

    // --- Authorization sessions (in-flight) --------------------------------

    public void SaveSession(AuthSession session)
    {
        EvictExpiredTransients();
        _sessions[session.State] = session;
    }

    public AuthSession? TakeSession(string state) =>
        _sessions.TryRemove(state, out var session) ? session : null;

    // --- Authorization codes (single-use) ----------------------------------

    public void SaveCode(AuthorizationCode code)
    {
        EvictExpiredTransients();
        _codes[code.Code] = code;
    }

    public AuthorizationCode? TakeCode(string code) =>
        _codes.TryRemove(code, out var value) ? value : null;

    // --- Tokens ------------------------------------------------------------

    public async Task SaveTokenAsync(TokenRecord token, CancellationToken cancellationToken = default)
    {
        string snapshot;
        lock (_sync)
        {
            _tokens[token.AccessToken] = token;
            PruneExpiredTokens();
            snapshot = JsonSerializer.Serialize(_tokens, JsonOptions);
        }
        await PersistAsync(_tokensFile, snapshot, cancellationToken).ConfigureAwait(false);
    }

    public TokenRecord? GetByAccessToken(string accessToken)
    {
        lock (_sync)
            return _tokens.TryGetValue(accessToken, out var token) ? token : null;
    }

    public TokenRecord? GetByRefreshToken(string refreshToken)
    {
        lock (_sync)
            return _tokens.Values.FirstOrDefault(t => t.RefreshToken == refreshToken);
    }

    public async Task ReplaceTokenAsync(string oldAccessToken, TokenRecord newToken, CancellationToken cancellationToken = default)
    {
        string snapshot;
        lock (_sync)
        {
            _tokens.Remove(oldAccessToken);
            _tokens[newToken.AccessToken] = newToken;
            snapshot = JsonSerializer.Serialize(_tokens, JsonOptions);
        }
        await PersistAsync(_tokensFile, snapshot, cancellationToken).ConfigureAwait(false);
    }

    // --- Helpers -----------------------------------------------------------

    /// <summary>
    /// Removes issued token records that can no longer be used: the access token is expired and there
    /// is no refresh token to renew it. Records with a refresh token are retained so refresh still works.
    /// Caller must hold <see cref="_sync"/>.
    /// </summary>
    private void PruneExpiredTokens()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var dead = _tokens
            .Where(kvp => kvp.Value.AccessTokenExpiresAt <= now && string.IsNullOrEmpty(kvp.Value.RefreshToken))
            .Select(kvp => kvp.Key)
            .ToList();
        foreach (var key in dead)
            _tokens.Remove(key);
    }

    /// <summary>
    /// Removes in-flight sessions and unconsumed codes older than <see cref="TransientLifetime"/>,
    /// bounding memory for a long-running server even if clients abandon authorization flows.
    /// </summary>
    private void EvictExpiredTransients()
    {
        var cutoff = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - (long)TransientLifetime.TotalSeconds;
        foreach (var (key, session) in _sessions)
        {
            if (session.CreatedAt < cutoff)
                _sessions.TryRemove(key, out _);
        }
        foreach (var (key, code) in _codes)
        {
            if (code.CreatedAt < cutoff)
                _codes.TryRemove(key, out _);
        }
    }

    private static T? Load<T>(string path) where T : class
    {
        if (!File.Exists(path))
            return null;
        try
        {
            var json = File.ReadAllText(path);
            return string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch (JsonException)
        {
            // Corrupt file: start fresh rather than crash the server.
            return null;
        }
    }

    private async Task PersistAsync(string path, string json, CancellationToken cancellationToken)
    {
        // Serialize file writes so concurrent saves can't interleave temp-file writes/moves.
        await _fileGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var tempPath = path + ".tmp";
            await File.WriteAllTextAsync(tempPath, json, cancellationToken).ConfigureAwait(false);
            File.Move(tempPath, path, overwrite: true);
        }
        finally
        {
            _fileGate.Release();
        }
    }
}
