namespace DocumentService.Core.Enums;

/// <summary>
/// Formats supported by the Export pipeline. Extend this enum and register a
/// matching IExportEngine + factory mapping to add a new format (e.g. Html, Json, Xml).
/// </summary>
public enum ExportFormat
{
    Excel,
    Csv,
    Word,
    Pdf,
    Markdown
}
