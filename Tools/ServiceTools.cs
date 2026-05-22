using ModelContextProtocol.Server;
using NinjaOne.Client.V2.Queries.WindowsServices;
using System.ComponentModel;

namespace Bezalu.NinjaOne.MCP.Tools;

/// <summary>
/// MCP tools for querying Windows services across managed devices.
/// </summary>
internal sealed class ServiceTools(NinjaOneClient client)
{
    [McpServerTool(Name = "query_windows_services")]
    [Description("Query Windows services across all managed devices. Filter by service name or state (RUNNING, STOPPED, PAUSED, UNKNOWN, START_PENDING, STOP_PENDING, PAUSE_PENDING, CONTINUE_PENDING).")]
    public async Task<string> QueryWindowsServicesAsync(
        [Description("Service name filter (exact or partial match)")] string? name = null,
        [Description("Service state filter (RUNNING, STOPPED, PAUSED, etc.)")] string? state = null,
        [Description("Device filter (optional NinjaOne device filter expression)")] string? deviceFilter = null,
        [Description("Page size (default 50)")] int? pageSize = 50,
        [Description("Cursor for pagination")] string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        var result = await client.V2.Queries.WindowsServices.GetAsync(q =>
        {
            q.QueryParameters.Name = name;
            q.QueryParameters.State = state is not null
                ? Enum.Parse<GetStateQueryParameterType>(state, ignoreCase: true)
                : null;
            q.QueryParameters.Df = deviceFilter;
            q.QueryParameters.PageSize = pageSize;
            q.QueryParameters.Cursor = cursor;
        }, cancellationToken: cancellationToken);

        return Json.Serialize(result);
    }
}
