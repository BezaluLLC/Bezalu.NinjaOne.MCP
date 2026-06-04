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

    private readonly string _clientsFile;
    private readonly string _tokensFile;
    private readonly SemaphoreSlim _gate = new(1, 1);

    // Short-lived, in-memory only (do not need to survive restarts).
    private readonly ConcurrentDictionary<string, AuthSession> _sessions = new();
    private readonly ConcurrentDictionary<string, AuthorizationCode> _codes = new();

    private Dictionary<string, RegisteredClient> _clients = [];
    private Dictionary<string, TokenRecord> _tokens = [];

    public FileOAuthStore(OAuthServerOptions options)
    {
        var dir = options.DataPath;
        Directory.CreateDirectory(dir);
        _clientsFile = Path.Combine(dir, "clients.json");
        _tokensFile = Path.Combine(dir, "tokens.json");

        _clients = Load<Dictionary<string, RegisteredClient>>(_clientsFile) ?? [];
        _tokens = Load<Dictionary<string, TokenRecord>>(_tokensFile) ?? [];
    }

    // --- Clients -----------------------------------------------------------

    public async Task SaveClientAsync(RegisteredClient client, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _clients[client.ClientId] = client;
            await PersistAsync(_clientsFile, _clients, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public RegisteredClient? GetClient(string clientId) =>
        _clients.TryGetValue(clientId, out var client) ? client : null;

    // --- Authorization sessions (in-flight) --------------------------------

    public void SaveSession(AuthSession session) => _sessions[session.State] = session;

    public AuthSession? TakeSession(string state) =>
        _sessions.TryRemove(state, out var session) ? session : null;

    // --- Authorization codes (single-use) ----------------------------------

    public void SaveCode(AuthorizationCode code) => _codes[code.Code] = code;

    public AuthorizationCode? TakeCode(string code) =>
        _codes.TryRemove(code, out var value) ? value : null;

    // --- Tokens ------------------------------------------------------------

    public async Task SaveTokenAsync(TokenRecord token, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _tokens[token.AccessToken] = token;
            await PersistAsync(_tokensFile, _tokens, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public TokenRecord? GetByAccessToken(string accessToken) =>
        _tokens.TryGetValue(accessToken, out var token) ? token : null;

    public TokenRecord? GetByRefreshToken(string refreshToken) =>
        _tokens.Values.FirstOrDefault(t => t.RefreshToken == refreshToken);

    public async Task ReplaceTokenAsync(string oldAccessToken, TokenRecord newToken, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _tokens.Remove(oldAccessToken);
            _tokens[newToken.AccessToken] = newToken;
            await PersistAsync(_tokensFile, _tokens, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    // --- Helpers -----------------------------------------------------------

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

    private static async Task PersistAsync<T>(string path, T value, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(value, JsonOptions);
        var tempPath = path + ".tmp";
        await File.WriteAllTextAsync(tempPath, json, cancellationToken).ConfigureAwait(false);
        File.Move(tempPath, path, overwrite: true);
    }
}
