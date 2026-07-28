using System.Text.Json;

namespace DocumentService.Core.Models;

/// <summary>
/// Converts between the caller-facing JSON shape (Title/Columns/Rows) and DocumentModel.
/// Centralized here so Export, Import and Print all agree on the same JSON contract,
/// and so row values come out as plain CLR types (string/double/bool/null) rather than
/// raw JsonElement, which every engine would otherwise have to unwrap itself.
/// </summary>
public static class DocumentJsonMapper
{
    private static readonly JsonSerializerOptions SerializeOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static DocumentModel FromJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var model = new DocumentModel
        {
            Title = GetString(root, "Title") ?? string.Empty,
            Company = GetString(root, "Company"),
            Header = GetString(root, "Header"),
            Footer = GetString(root, "Footer")
        };

        if (root.TryGetProperty("Columns", out var columnsEl) && columnsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var col in columnsEl.EnumerateArray())
            {
                model.Columns.Add(new DocumentColumn
                {
                    Header = GetString(col, "Header") ?? string.Empty,
                    Field = GetString(col, "Field") ?? string.Empty
                });
            }
        }

        if (root.TryGetProperty("Rows", out var rowsEl) && rowsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var row in rowsEl.EnumerateArray())
            {
                model.Rows.Add(JsonValueConverter.ToDictionary(row));
            }
        }

        return model;
    }

    public static string ToJson(DocumentModel model)
    {
        var payload = new
        {
            model.Title,
            model.Company,
            model.Header,
            model.Footer,
            Columns = model.Columns,
            Rows = model.Rows
        };
        return JsonSerializer.Serialize(payload, SerializeOptions);
    }

    private static string? GetString(JsonElement el, string name) =>
        el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
