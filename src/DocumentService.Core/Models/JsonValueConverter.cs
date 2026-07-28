using System.Text.Json;

namespace DocumentService.Core.Models;

/// <summary>
/// Converts arbitrary JSON (JsonElement) into plain CLR types (string/long/double/bool/null,
/// nested Dictionary&lt;string, object?&gt; and List&lt;object?&gt;). Shared by DocumentJsonMapper
/// (fixed Title/Columns/Rows shape) and PrintService (free-form template data, e.g. an Items array).
/// </summary>
public static class JsonValueConverter
{
    public static Dictionary<string, object?> ParseObject(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return ToDictionary(doc.RootElement);
    }

    public static Dictionary<string, object?> ToDictionary(JsonElement element)
    {
        var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in element.EnumerateObject())
        {
            dict[prop.Name] = ToClrValue(prop.Value);
        }
        return dict;
    }

    public static object? ToClrValue(JsonElement el) => el.ValueKind switch
    {
        JsonValueKind.String => el.GetString(),
        JsonValueKind.Number => el.TryGetInt64(out var l) ? l : el.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null or JsonValueKind.Undefined => null,
        JsonValueKind.Object => ToDictionary(el),
        JsonValueKind.Array => el.EnumerateArray().Select(ToClrValue).ToList(),
        _ => null
    };
}
