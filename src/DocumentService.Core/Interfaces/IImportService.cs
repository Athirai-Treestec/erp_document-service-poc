using DocumentService.Core.DTOs;

namespace DocumentService.Core.Interfaces;

public interface IImportService
{
    Task<ImportResult> ImportAsync(ImportRequest request, CancellationToken cancellationToken = default);
}
