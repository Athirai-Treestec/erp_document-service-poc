using DocumentService.Core.Enums;
using DocumentService.Core.Models;

namespace DocumentService.Core.Interfaces;

/// <summary>
/// Contract implemented once per supported ExportFormat (Excel, Csv, Word, Pdf, Markdown).
/// Each implementation owns exactly one third-party library so that library can be
/// swapped out later without touching IExportService or the Factory.
/// </summary>
public interface IExportEngine
{
    ExportFormat Format { get; }
    Task<byte[]> GenerateAsync(DocumentModel document, CancellationToken cancellationToken = default);
}
