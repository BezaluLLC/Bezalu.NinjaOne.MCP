using ModelContextProtocol.Server;
using System.ComponentModel;

namespace Bezalu.NinjaOne.MCP.Tools;

/// <summary>
/// MCP tools for viewing automation tasks.
/// </summary>
internal sealed class TaskTools(NinjaOneClient client)
{
    [McpServerTool(Name = "list_tasks", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("List all automation tasks configured in the NinjaOne instance.")]
    public async Task<string> ListTasksAsync(CancellationToken cancellationToken = default)
    {
        var tasks = await client.V2.Tasks.GetAsync(cancellationToken: cancellationToken);
        return Json.Serialize(tasks);
    }
}
