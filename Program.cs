using Bezalu.NinjaOne.MCP.Auth;
using Bezalu.NinjaOne.MCP.Auth.OAuth;
using Bezalu.NinjaOne.MCP.Tools;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.HttpOverrides;
using ModelContextProtocol.AspNetCore.Authentication;
using ModelContextProtocol.Authentication;

var builder = WebApplication.CreateBuilder(args);

// Bind OAuth bridge + authorization-server configuration from environment.
var oauthOptions = OAuthServerOptions.FromConfiguration(builder.Configuration);
builder.Services.AddSingleton(oauthOptions);
builder.Services.AddSingleton<FileOAuthStore>();
builder.Services.AddSingleton<NinjaOneTokenBridge>();

// Honor forwarded headers so issuer/redirect URLs reflect the external host behind a proxy.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

// Configure MCP authentication — the MCP handler serves resource metadata and challenges.
// The bearer handler validates tokens this server issued and resolves the user's NinjaOne token.
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = "Bearer";
    options.DefaultChallengeScheme = McpAuthenticationDefaults.AuthenticationScheme;
})
.AddMcp(options =>
{
    // The authorization server is this MCP server itself (DCR-capable). The issuer is resolved
    // per-request so it reflects the external URL even behind a reverse proxy.
    options.Events.OnResourceMetadataRequest = context =>
    {
        var issuer = OAuthEndpoints.GetIssuer(context.HttpContext);
        context.ResourceMetadata = new ProtectedResourceMetadata
        {
            AuthorizationServers = { issuer },
            ScopesSupported = [.. oauthOptions.Scopes.Split(' ', StringSplitOptions.RemoveEmptyEntries)],
        };
        return Task.CompletedTask;
    };
})
.AddScheme<AuthenticationSchemeOptions, IssuedTokenBearerHandler>("Bearer", null);

builder.Services.AddAuthorization();

// HttpClient for NinjaOne API calls
builder.Services.AddHttpClient("NinjaOne");
builder.Services.AddHttpContextAccessor();

// Scoped NinjaOneClient resolved from the NinjaOne access token the bearer handler resolved for
// the current request (kept per-user; never the shared app credential).
builder.Services.AddScoped<NinjaOneClient>(sp =>
{
    var httpContext = sp.GetRequiredService<IHttpContextAccessor>().HttpContext
        ?? throw new InvalidOperationException("No active HTTP request");
    var token = httpContext.Items[IssuedTokenBearerHandler.NinjaTokenItemKey] as string;
    if (string.IsNullOrWhiteSpace(token))
        throw new InvalidOperationException("No NinjaOne access token available for this request");
    var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient("NinjaOne");
    return NinjaOneClient.Create(oauthOptions.InstanceUrl, token, httpClient);
});

// MCP server with tools
builder.Services
    .AddMcpServer()
    .WithHttpTransport(options =>
    {
        options.Stateless = true;
    })
    .WithTools<DeviceTools>()
    .WithTools<OrganizationTools>()
    .WithTools<AlertTools>()
    .WithTools<LocationTools>()
    .WithTools<CustomFieldTools>()
    .WithTools<SoftwareTools>()
    .WithTools<ServiceTools>()
    .WithTools<JobTools>()
    .WithTools<TaskTools>()
    .WithTools<SecurityTools>();

var app = builder.Build();

app.UseForwardedHeaders();
app.UseAuthentication();
app.UseAuthorization();

// Embedded OAuth 2.0 authorization server endpoints (DCR, authorize, callback, token, metadata).
app.MapOAuthServer();

app.MapMcp().RequireAuthorization();

app.Run();

