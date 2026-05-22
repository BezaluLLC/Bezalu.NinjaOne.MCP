using ModelContextProtocol.Server;
using System.ComponentModel;

namespace Bezalu.NinjaOne.MCP.Tools;

/// <summary>
/// MCP tools for viewing scheduled and running jobs.
/// </summary>
internal sealed class JobTools(NinjaOneClient client)
{
    [McpServerTool(Name = "list_jobs", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("List scheduled and running jobs across the NinjaOne instance. Includes scripted tasks, patching jobs, and maintenance.")]
    public async Task<string> ListJobsAsync(
        [Description("Job type filter (e.g., actionset, script)")] string? jobType = null,
        [Description("Device filter (optional NinjaOne device filter expression)")] string? deviceFilter = null,
        [Description("Language tag for job names (default en)")] string? lang = "en",
        CancellationToken cancellationToken = default)
    {
        var jobs = await client.V2.Jobs.GetAsync(q =>
        {
            q.QueryParameters.JobType = jobType;
            q.QueryParameters.Df = deviceFilter;
            q.QueryParameters.Lang = lang;
        }, cancellationToken: cancellationToken);

        return Json.Serialize(jobs);
    }
}
