using DocumentService.Core.DTOs;
using DocumentService.Core.Exceptions;
using DocumentService.Core.Interfaces;
using DocumentService.Core.Models;
using Microsoft.Extensions.Logging;

namespace DocumentService.Engines.Services;

/// <summary>
/// Picks the right IImportEngine via the factory, reads the uploaded file into a
/// DocumentModel, and serializes it back out as JSON. Nothing is persisted.
/// </summary>
public class ImportService : IImportService
{
    private readonly IImportEngineFactory _engineFactory;
    private readonly ILogger<ImportService> _logger;

    public ImportService(IImportEngineFactory engineFactory, ILogger<ImportService> logger)
    {
        _engineFactory = engineFactory;
        _logger = logger;
    }

    public async Task<ImportResult> ImportAsync(ImportRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Content.Length == 0)
        {
            throw new DocumentServiceException("The uploaded file is empty.");
        }

        var engine = _engineFactory.GetEngine(request.Format);

        using var stream = new MemoryStream(request.Content);
        var document = await engine.ReadAsync(stream, cancellationToken);

        _logger.LogInformation("Imported '{FileName}' as {Format} ({Rows} rows)", request.FileName, request.Format, document.Rows.Count);

        return new ImportResult
        {
            Json = DocumentJsonMapper.ToJson(document)
        };
    }
}
