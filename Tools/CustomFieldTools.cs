using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace Bezalu.NinjaOne.MCP.Tools;

/// <summary>
/// MCP tools for managing NinjaOne custom fields.
/// </summary>
internal sealed class CustomFieldTools(NinjaOneClient client)
{
    [McpServerTool(Name = "list_custom_fields", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("List all custom field definitions configured in the NinjaOne instance.")]
    public async Task<string> ListCustomFieldsAsync(CancellationToken cancellationToken = default)
    {
        var fields = await client.V2.CustomFields.GetAsync(cancellationToken: cancellationToken);
        return Json.Serialize(fields);
    }

    [McpServerTool(Name = "get_device_custom_fields", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Get custom field values for a specific device.")]
    public async Task<string> GetDeviceCustomFieldsAsync(
        [Description("The NinjaOne device ID")] int deviceId,
        CancellationToken cancellationToken = default)
    {
        var fields = await client.V2.Device[deviceId].CustomFields.GetAsync(cancellationToken: cancellationToken);
        return Json.Serialize(fields);
    }

    [McpServerTool(Name = "update_device_custom_fields", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Update custom field values for a specific device. Pass field names and values as a JSON object.")]
    public async Task<string> UpdateDeviceCustomFieldsAsync(
        [Description("The NinjaOne device ID")] int deviceId,
        [Description("JSON object with field names as keys and values to set")] string fieldsJson,
        CancellationToken cancellationToken = default)
    {
        var fields = JsonSerializer.Deserialize<Dictionary<string, object>>(fieldsJson) ?? [];
        var body = new global::NinjaOne.Client.V2.Device.Item.CustomFields.CustomFieldsPatchRequestBody
        {
            AdditionalData = fields,
        };
        await client.V2.Device[deviceId].CustomFields.PatchAsync(body, cancellationToken: cancellationToken);
        return $"Custom fields updated successfully for device {deviceId}.";
    }
}
