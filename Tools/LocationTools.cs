using ModelContextProtocol.Server;
using System.ComponentModel;

namespace Bezalu.NinjaOne.MCP.Tools;

/// <summary>
/// MCP tools for managing NinjaOne locations.
/// </summary>
internal sealed class LocationTools(NinjaOneClient client)
{
    [McpServerTool(Name = "list_locations", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("List all locations across all organizations, or for a specific organization.")]
    public async Task<string> ListLocationsAsync(
        [Description("Filter by organization ID (optional)")] int? organizationId = null,
        [Description("Page size (default 50)")] int? pageSize = 50,
        [Description("Cursor for pagination")] string? after = null,
        CancellationToken cancellationToken = default)
    {

        if (organizationId.HasValue)
        {
            var orgLocations = await client.V2.Organization[organizationId.Value].Locations.GetAsync(
                cancellationToken: cancellationToken);
            return Json.Serialize(orgLocations);
        }

        var locations = await client.V2.Locations.GetAsync(q =>
        {
            q.QueryParameters.PageSize = pageSize;
            q.QueryParameters.After = after != null ? int.Parse(after) : null;
        }, cancellationToken: cancellationToken);

        return Json.Serialize(locations);
    }
}
