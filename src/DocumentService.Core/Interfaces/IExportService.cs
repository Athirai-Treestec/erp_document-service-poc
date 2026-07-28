using DocumentService.Core.DTOs;

namespace DocumentService.Core.Interfaces;

public interface IExportService
{
    Task<ExportResult> ExportAsync(ExportRequest request, CancellationToken cancellationToken = default);
}
