using ModelContextProtocol.Server;
using System.ComponentModel;

namespace Bezalu.NinjaOne.MCP.Tools;

/// <summary>
/// MCP tools for managing NinjaOne organizations.
/// </summary>
internal sealed class OrganizationTools(NinjaOneClient client)
{
    [McpServerTool(Name = "list_organizations")]
    [Description("List all organizations in the NinjaOne instance.")]
    public async Task<string> ListOrganizationsAsync(
        [Description("Page size (default 50)")] int? pageSize = 50,
        [Description("Cursor for pagination")] string? after = null,
        CancellationToken cancellationToken = default)
    {
        var orgs = await client.V2.Organizations.GetAsync(q =>
        {
            q.QueryParameters.PageSize = pageSize;
            q.QueryParameters.After = after != null ? int.Parse(after) : null;
        }, cancellationToken: cancellationToken);

        return Json.Serialize(orgs);
    }

    [McpServerTool(Name = "list_organizations_detailed")]
    [Description("List all organizations with detailed information including policies, locations, and settings.")]
    public async Task<string> ListOrganizationsDetailedAsync(
        [Description("Page size (default 50)")] int? pageSize = 50,
        [Description("Cursor for pagination")] string? after = null,
        CancellationToken cancellationToken = default)
    {
        var orgs = await client.V2.OrganizationsDetailed.GetAsync(q =>
        {
            q.QueryParameters.PageSize = pageSize;
            q.QueryParameters.After = after != null ? int.Parse(after) : null;
        }, cancellationToken: cancellationToken);

        return Json.Serialize(orgs);
    }

    [McpServerTool(Name = "get_organization")]
    [Description("Get detailed information about a specific organization by its ID.")]
    public async Task<string> GetOrganizationAsync(
        [Description("The NinjaOne organization ID")] int organizationId,
        CancellationToken cancellationToken = default)
    {
        var org = await client.V2.Organization[organizationId].GetAsync(cancellationToken: cancellationToken);
        return Json.Serialize(org);
    }
}
