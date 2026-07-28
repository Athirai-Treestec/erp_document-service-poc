namespace DocumentService.Core.Models;

/// <summary>
/// The single, engine-agnostic representation of tabular document data.
/// Every export/import engine reads and produces this shape only —
/// no engine ever sees the caller's original JSON or a third-party library type.
/// </summary>
public class DocumentModel
{
    public string Title { get; set; } = string.Empty;
    public string? Company { get; set; }
    public string? Header { get; set; }
    public string? Footer { get; set; }

    public List<DocumentColumn> Columns { get; set; } = new();

    /// <summary>Each row is Field -> value, keyed by DocumentColumn.Field.</summary>
    public List<Dictionary<string, object?>> Rows { get; set; } = new();

    public DocumentOptions Options { get; set; } = new();
}
