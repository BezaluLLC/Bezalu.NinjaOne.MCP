using System.Text.Json;

namespace Bezalu.NinjaOne.MCP.Tools;

/// <summary>
/// Shared JSON serialization options for MCP tool responses.
/// </summary>
internal static class Json
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);
}
