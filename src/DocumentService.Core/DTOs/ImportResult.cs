namespace DocumentService.Core.DTOs;

/// <summary>
/// Output of an import: the resulting JSON (Columns + Rows) that the caller can
/// deserialize or forward as-is. Nothing is persisted by the import pipeline.
/// </summary>
public class ImportResult
{
    public string Json { get; set; } = string.Empty;
}
