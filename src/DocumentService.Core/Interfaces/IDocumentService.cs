using DocumentService.Core.DTOs;

namespace DocumentService.Core.Interfaces;

/// <summary>
/// The single facade an ERP module talks to. It never exposes engines, factories,
/// or third-party types — only Export/Import/Print DTOs built from plain JSON and bytes.
/// </summary>
public interface IDocumentService
{
    Task<ExportResult> ExportAsync(ExportRequest request, CancellationToken cancellationToken = default);
    Task<ImportResult> ImportAsync(ImportRequest request, CancellationToken cancellationToken = default);
    Task<PrintResult> PrintAsync(PrintRequest request, CancellationToken cancellationToken = default);
}
