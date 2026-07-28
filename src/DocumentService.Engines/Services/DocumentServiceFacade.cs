using DocumentService.Core.DTOs;
using DocumentService.Core.Interfaces;

namespace DocumentService.Engines.Services;

/// <summary>
/// The single entry point an ERP module injects. It just forwards to the three
/// underlying services — its only job is to be the one thing business code depends on,
/// so Export/Import/Print internals (engines, factories, renderers) stay invisible.
/// </summary>
public class DocumentServiceFacade : IDocumentService
{
    private readonly IExportService _exportService;
    private readonly IImportService _importService;
    private readonly IPrintService _printService;

    public DocumentServiceFacade(IExportService exportService, IImportService importService, IPrintService printService)
    {
        _exportService = exportService;
        _importService = importService;
        _printService = printService;
    }

    public Task<ExportResult> ExportAsync(ExportRequest request, CancellationToken cancellationToken = default) =>
        _exportService.ExportAsync(request, cancellationToken);

    public Task<ImportResult> ImportAsync(ImportRequest request, CancellationToken cancellationToken = default) =>
        _importService.ImportAsync(request, cancellationToken);

    public Task<PrintResult> PrintAsync(PrintRequest request, CancellationToken cancellationToken = default) =>
        _printService.PrintAsync(request, cancellationToken);
}
