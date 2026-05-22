using ModelContextProtocol.Server;
using System.ComponentModel;

namespace Bezalu.NinjaOne.MCP.Tools;

/// <summary>
/// MCP tools for RMM security and health queries — antivirus, OS patching, device health.
/// </summary>
internal sealed class SecurityTools(NinjaOneClient client)
{
    [McpServerTool(Name = "query_antivirus_status", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Query antivirus protection status across all managed devices. Shows product name, status, and definition age.")]
    public async Task<string> QueryAntivirusStatusAsync(
        [Description("Device filter (optional NinjaOne device filter expression)")] string? deviceFilter = null,
        [Description("Page size (default 50)")] int? pageSize = 50,
        [Description("Cursor for pagination")] string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        var result = await client.V2.Queries.AntivirusStatus.GetAsync(q =>
        {
            q.QueryParameters.Df = deviceFilter;
            q.QueryParameters.PageSize = pageSize;
            q.QueryParameters.Cursor = cursor;
        }, cancellationToken: cancellationToken);

        return Json.Serialize(result);
    }

    [McpServerTool(Name = "query_antivirus_threats", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Query detected antivirus threats across all managed devices.")]
    public async Task<string> QueryAntivirusThreatsAsync(
        [Description("Device filter (optional NinjaOne device filter expression)")] string? deviceFilter = null,
        [Description("Page size (default 50)")] int? pageSize = 50,
        [Description("Cursor for pagination")] string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        var result = await client.V2.Queries.AntivirusThreats.GetAsync(q =>
        {
            q.QueryParameters.Df = deviceFilter;
            q.QueryParameters.PageSize = pageSize;
            q.QueryParameters.Cursor = cursor;
        }, cancellationToken: cancellationToken);

        return Json.Serialize(result);
    }

    [McpServerTool(Name = "query_os_patches", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Query available OS patches across all managed devices. Shows pending Windows/macOS updates.")]
    public async Task<string> QueryOsPatchesAsync(
        [Description("Device filter (optional NinjaOne device filter expression)")] string? deviceFilter = null,
        [Description("Page size (default 50)")] int? pageSize = 50,
        [Description("Cursor for pagination")] string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        var result = await client.V2.Queries.OsPatches.GetAsync(q =>
        {
            q.QueryParameters.Df = deviceFilter;
            q.QueryParameters.PageSize = pageSize;
            q.QueryParameters.Cursor = cursor;
        }, cancellationToken: cancellationToken);

        return Json.Serialize(result);
    }

    [McpServerTool(Name = "query_os_patch_installs", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Query OS patch installation history across devices. Shows what OS updates were installed and when.")]
    public async Task<string> QueryOsPatchInstallsAsync(
        [Description("Device filter (optional NinjaOne device filter expression)")] string? deviceFilter = null,
        [Description("Page size (default 50)")] int? pageSize = 50,
        [Description("Cursor for pagination")] string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        var result = await client.V2.Queries.OsPatchInstalls.GetAsync(q =>
        {
            q.QueryParameters.Df = deviceFilter;
            q.QueryParameters.PageSize = pageSize;
            q.QueryParameters.Cursor = cursor;
        }, cancellationToken: cancellationToken);

        return Json.Serialize(result);
    }

    [McpServerTool(Name = "query_device_health")]
    [Description("Query overall device health status across all managed devices.")]
    public async Task<string> QueryDeviceHealthAsync(
        [Description("Device filter (optional NinjaOne device filter expression)")] string? deviceFilter = null,
        [Description("Page size (default 50)")] int? pageSize = 50,
        [Description("Cursor for pagination")] string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        var result = await client.V2.Queries.DeviceHealth.GetAsync(q =>
        {
            q.QueryParameters.Df = deviceFilter;
            q.QueryParameters.PageSize = pageSize;
            q.QueryParameters.Cursor = cursor;
        }, cancellationToken: cancellationToken);

        return Json.Serialize(result);
    }
}
