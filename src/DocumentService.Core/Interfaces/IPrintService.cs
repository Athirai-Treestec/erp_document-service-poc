using DocumentService.Core.DTOs;

namespace DocumentService.Core.Interfaces;

public interface IPrintService
{
    Task<PrintResult> PrintAsync(PrintRequest request, CancellationToken cancellationToken = default);
}
