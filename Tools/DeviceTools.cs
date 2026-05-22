using ModelContextProtocol.Server;
using System.ComponentModel;

namespace Bezalu.NinjaOne.MCP.Tools;

/// <summary>
/// MCP tools for managing NinjaOne devices.
/// </summary>
internal sealed class DeviceTools(NinjaOneClient client)
{
    [McpServerTool(Name = "list_devices")]
    [Description("List all devices in the NinjaOne instance. Returns device ID, system name, organization, and status.")]
    public async Task<string> ListDevicesAsync(
        [Description("Page size (default 50, max 1000)")] int? pageSize = 50,
        [Description("Cursor for pagination")] string? after = null,
        CancellationToken cancellationToken = default)
    {
        var devices = await client.V2.Devices.GetAsync(q =>
        {
            q.QueryParameters.PageSize = pageSize;
            q.QueryParameters.After = after != null ? int.Parse(after) : null;
        }, cancellationToken: cancellationToken);

        return Json.Serialize(devices);
    }

    [McpServerTool(Name = "list_devices_detailed")]
    [Description("List all devices with detailed information including OS, last contact time, and hardware details.")]
    public async Task<string> ListDevicesDetailedAsync(
        [Description("Page size (default 50, max 1000)")] int? pageSize = 50,
        [Description("Cursor for pagination")] string? after = null,
        CancellationToken cancellationToken = default)
    {
        var devices = await client.V2.DevicesDetailed.GetAsync(q =>
        {
            q.QueryParameters.PageSize = pageSize;
            q.QueryParameters.After = after != null ? int.Parse(after) : null;
        }, cancellationToken: cancellationToken);

        return Json.Serialize(devices);
    }

    [McpServerTool(Name = "get_device")]
    [Description("Get detailed information about a specific device by its ID.")]
    public async Task<string> GetDeviceAsync(
        [Description("The NinjaOne device ID")] int deviceId,
        CancellationToken cancellationToken = default)
    {
        var device = await client.V2.Device[deviceId].GetAsync(cancellationToken: cancellationToken);
        return Json.Serialize(device);
    }

    [McpServerTool(Name = "search_devices")]
    [Description("Search for devices by name or other criteria.")]
    public async Task<string> SearchDevicesAsync(
        [Description("Search query string")] string query,
        [Description("Maximum results to return (default 50)")] int? limit = 50,
        CancellationToken cancellationToken = default)
    {
        var results = await client.V2.Devices.Search.GetAsync(q =>
        {
            q.QueryParameters.Q = query;
            q.QueryParameters.Limit = limit;
        }, cancellationToken: cancellationToken);

        return Json.Serialize(results);
    }
}
