using DocumentService.Core.Enums;

namespace DocumentService.Core.DTOs;

/// <summary>
/// Input to IExportService: the caller's JSON payload (matching DocumentModel's shape)
/// plus the desired output format.
/// </summary>
public class ExportRequest
{
    public string Json { get; set; } = string.Empty;
    public ExportFormat Format { get; set; }
}
