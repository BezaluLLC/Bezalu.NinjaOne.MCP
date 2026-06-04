using Bezalu.NinjaOne.MCP.Auth.OAuth;
using Xunit;

namespace Bezalu.NinjaOne.MCP.Tests;

public class FileOAuthStoreTests : IDisposable
{
    private readonly string _dataPath;
    private readonly OAuthServerOptions _options;

    public FileOAuthStoreTests()
    {
        _dataPath = Path.Combine(Path.GetTempPath(), "ninjaone-mcp-tests", Guid.NewGuid().ToString("N"));
        _options = new OAuthServerOptions
        {
            InstanceUrl = "https://app.ninjarmm.com",
            NinjaClientId = "client",
            NinjaClientSecret = "secret",
            Scopes = "monitoring management offline_access",
            DataPath = _dataPath,
        };
    }

    public void Dispose()
    {
        if (Directory.Exists(_dataPath))
            Directory.Delete(_dataPath, recursive: true);
        GC.SuppressFinalize(this);
    }

    private static RegisteredClient NewClient(string id) => new()
    {
        ClientId = id,
        RedirectUris = ["https://client.example/callback"],
        ClientName = "Test Client",
    };

    private static TokenRecord NewToken(string accessToken, string refreshToken) => new()
    {
        AccessToken = accessToken,
        RefreshToken = refreshToken,
        ClientId = "client",
        Scope = "monitoring",
        NinjaAccessToken = "ninja-access",
        NinjaRefreshToken = "ninja-refresh",
        NinjaAccessTokenExpiresAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 3600,
        AccessTokenExpiresAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 3600,
    };

    [Fact]
    public async Task SaveAndGetClient_RoundTrips()
    {
        var store = new FileOAuthStore(_options);
        var client = NewClient("abc");

        await store.SaveClientAsync(client);

        Assert.Equal("abc", store.GetClient("abc")!.ClientId);
        Assert.Null(store.GetClient("missing"));
    }

    [Fact]
    public async Task Clients_PersistAcrossInstances()
    {
        var store = new FileOAuthStore(_options);
        await store.SaveClientAsync(NewClient("persisted"));

        var reopened = new FileOAuthStore(_options);

        Assert.NotNull(reopened.GetClient("persisted"));
    }

    [Fact]
    public void Session_IsSingleUse()
    {
        var store = new FileOAuthStore(_options);
        var session = new AuthSession
        {
            State = "state-1",
            ClientId = "client",
            ClientRedirectUri = "https://client.example/callback",
            CodeChallenge = "challenge",
            Scope = "monitoring",
        };

        store.SaveSession(session);

        Assert.Equal("client", store.TakeSession("state-1")!.ClientId);
        Assert.Null(store.TakeSession("state-1"));
    }

    [Fact]
    public void Code_IsSingleUse()
    {
        var store = new FileOAuthStore(_options);
        var code = new AuthorizationCode
        {
            Code = "code-1",
            ClientId = "client",
            RedirectUri = "https://client.example/callback",
            CodeChallenge = "challenge",
            Scope = "monitoring",
            NinjaAccessToken = "ninja",
            NinjaAccessTokenExpiresAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 3600,
        };

        store.SaveCode(code);

        Assert.NotNull(store.TakeCode("code-1"));
        Assert.Null(store.TakeCode("code-1"));
    }

    [Fact]
    public async Task GetByRefreshToken_FindsRecord()
    {
        var store = new FileOAuthStore(_options);
        await store.SaveTokenAsync(NewToken("access-1", "refresh-1"));

        Assert.Equal("access-1", store.GetByRefreshToken("refresh-1")!.AccessToken);
        Assert.Null(store.GetByRefreshToken("nope"));
    }

    [Fact]
    public async Task ReplaceToken_SwapsAccessTokenKey()
    {
        var store = new FileOAuthStore(_options);
        await store.SaveTokenAsync(NewToken("old-access", "refresh-1"));

        var replacement = NewToken("new-access", "refresh-2");
        await store.ReplaceTokenAsync("old-access", replacement);

        Assert.Null(store.GetByAccessToken("old-access"));
        Assert.Equal("new-access", store.GetByAccessToken("new-access")!.AccessToken);
    }
}
