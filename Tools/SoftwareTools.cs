using System.ComponentModel;
using System.Globalization;
using ModelContextProtocol.Server;

namespace Bezalu.NinjaOne.MCP.Tools;

/// <summary>
/// MCP tools for querying installed software, products, and patch status.
/// </summary>
internal sealed class SoftwareTools(NinjaOneClient client)
{
    [McpServerTool(Name = "query_installed_software")]
    [Description("Query installed software across all devices. Returns software name, version, install date, and device info.")]
    public async Task<string> QueryInstalledSoftwareAsync(
        [Description("Device filter (optional NinjaOne device filter expression)")] string? deviceFilter = null,
        [Description("Only software installed after this date (yyyy-MM-dd)")] string? installedAfter = null,
        [Description("Only software installed before this date (yyyy-MM-dd)")] string? installedBefore = null,
        [Description("Page size (default 50)")] int? pageSize = 50,
        [Description("Cursor for pagination")] string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        const string dateFormat = "yyyy-MM-dd";
        if (installedAfter is not null && !DateOnly.TryParseExact(installedAfter, dateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            throw new ArgumentException($"Invalid installedAfter date '{installedAfter}'. Expected format: {dateFormat}", nameof(installedAfter));
        if (installedBefore is not null && !DateOnly.TryParseExact(installedBefore, dateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            throw new ArgumentException($"Invalid installedBefore date '{installedBefore}'. Expected format: {dateFormat}", nameof(installedBefore));

        var result = await client.V2.Queries.Software.GetAsync(q =>
        {
            q.QueryParameters.Df = deviceFilter;
            q.QueryParameters.InstalledAfter = installedAfter;
            q.QueryParameters.InstalledBefore = installedBefore;
            q.QueryParameters.PageSize = pageSize;
            q.QueryParameters.Cursor = cursor;
        }, cancellationToken: cancellationToken);

        return Json.Serialize(result);
    }

    [McpServerTool(Name = "list_software_products")]
    [Description("List all known software products tracked by NinjaOne.")]
    public async Task<string> ListSoftwareProductsAsync(CancellationToken cancellationToken = default)
    {
        var products = await client.V2.SoftwareProducts.GetAsync(cancellationToken: cancellationToken);
        return Json.Serialize(products);
    }

    [McpServerTool(Name = "query_software_patches")]
    [Description("Query available software patches across all devices. Shows pending patches by device.")]
    public async Task<string> QuerySoftwarePatchesAsync(
        [Description("Device filter (optional NinjaOne device filter expression)")] string? deviceFilter = null,
        [Description("Page size (default 50)")] int? pageSize = 50,
        [Description("Cursor for pagination")] string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        var result = await client.V2.Queries.SoftwarePatches.GetAsync(q =>
        {
            q.QueryParameters.Df = deviceFilter;
            q.QueryParameters.PageSize = pageSize;
            q.QueryParameters.Cursor = cursor;
        }, cancellationToken: cancellationToken);

        return Json.Serialize(result);
    }

    [McpServerTool(Name = "query_software_patch_installs")]
    [Description("Query software patch installation history across devices. Shows what patches were installed and when.")]
    public async Task<string> QuerySoftwarePatchInstallsAsync(
        [Description("Device filter (optional NinjaOne device filter expression)")] string? deviceFilter = null,
        [Description("Page size (default 50)")] int? pageSize = 50,
        [Description("Cursor for pagination")] string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        var result = await client.V2.Queries.SoftwarePatchInstalls.GetAsync(q =>
        {
            q.QueryParameters.Df = deviceFilter;
            q.QueryParameters.PageSize = pageSize;
            q.QueryParameters.Cursor = cursor;
        }, cancellationToken: cancellationToken);

        return Json.Serialize(result);
    }
}
