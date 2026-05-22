using ModelContextProtocol.Server;
using System.ComponentModel;

namespace Bezalu.NinjaOne.MCP.Tools;

/// <summary>
/// MCP tools for managing NinjaOne alerts.
/// </summary>
internal sealed class AlertTools(NinjaOneClient client)
{
    [McpServerTool(Name = "list_alerts", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("List active alerts across all devices and organizations. Returns alert severity, message, device, and timestamp.")]
    public async Task<string> ListAlertsAsync(
        [Description("Source type filter (e.g., CONDITION, CONDITION_ACTIONSET)")] string? sourceType = null,
        [Description("Language tag for alert messages (default en)")] string? lang = "en",
        CancellationToken cancellationToken = default)
    {
        var alerts = await client.V2.Alerts.GetAsync(q =>
        {
            q.QueryParameters.SourceType = sourceType;
            q.QueryParameters.Lang = lang;
        }, cancellationToken: cancellationToken);

        return Json.Serialize(alerts);
    }

    [McpServerTool(Name = "reset_alert", ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description("Reset/acknowledge a specific alert by its unique identifier.")]
    public async Task<string> ResetAlertAsync(
        [Description("The alert unique identifier (UID) as a GUID")] string alertUid,
        CancellationToken cancellationToken = default)
    {
        await client.V2.Alert[Guid.Parse(alertUid)].Reset.PostAsync(new global::NinjaOne.Client.V2.Alert.Item.Reset.ResetPostRequestBody(), cancellationToken: cancellationToken);
        return $"Alert {alertUid} has been reset successfully.";
    }
}
