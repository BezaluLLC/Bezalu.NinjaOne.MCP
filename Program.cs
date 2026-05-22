using Bezalu.NinjaOne.MCP.Tools;
using ModelContextProtocol.AspNetCore.Authentication;
using ModelContextProtocol.Authentication;

var builder = WebApplication.CreateBuilder(args);

var instanceUrl = builder.Configuration["NINJAONE_INSTANCE"]
    ?? throw new InvalidOperationException("NINJAONE_INSTANCE environment variable is required (e.g., https://app.ninjarmm.com)");
var scopes = builder.Configuration["NINJAONE_SCOPES"] ?? "monitoring management offline_access";

var oauthBaseUrl = instanceUrl.TrimEnd('/');

// Configure MCP authentication — the MCP handler serves resource metadata and challenges.
// The Bearer handler validates the actual token on requests.
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = "Bearer";
    options.DefaultChallengeScheme = McpAuthenticationDefaults.AuthenticationScheme;
})
.AddMcp(options =>
{
    options.ResourceMetadata = new ProtectedResourceMetadata
    {
        AuthorizationServers = { oauthBaseUrl },
        ScopesSupported = [.. scopes.Split(' ', StringSplitOptions.RemoveEmptyEntries)],
    };
})
.AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, Bezalu.NinjaOne.MCP.Auth.PassthroughBearerHandler>("Bearer", null);

builder.Services.AddAuthorization();

// HttpClient for NinjaOne API calls
builder.Services.AddHttpClient("NinjaOne");
builder.Services.AddHttpContextAccessor();

// Scoped NinjaOneClient resolved from the current request's bearer token
builder.Services.AddScoped<NinjaOneClient>(sp =>
{
    var httpContext = sp.GetRequiredService<IHttpContextAccessor>().HttpContext
        ?? throw new InvalidOperationException("No active HTTP request");
    var token = httpContext.Request.Headers.Authorization
        .ToString().Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase);
    if (string.IsNullOrWhiteSpace(token))
        throw new InvalidOperationException("No access token available");
    var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient("NinjaOne");
    return NinjaOneClient.Create(instanceUrl, token, httpClient);
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
    .WithTools<CustomFieldTools>();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
app.MapMcp().RequireAuthorization();

app.Run();

