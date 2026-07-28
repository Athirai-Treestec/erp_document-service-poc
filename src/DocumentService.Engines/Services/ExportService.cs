using DocumentService.Core.DTOs;
using DocumentService.Core.Enums;
using DocumentService.Core.Exceptions;
using DocumentService.Core.Interfaces;
using DocumentService.Core.Models;
using Microsoft.Extensions.Logging;

namespace DocumentService.Engines.Services;

/// <summary>
/// Converts the caller's JSON into a DocumentModel, picks the right IExportEngine
/// via the factory, and wraps the resulting bytes with a file name/content type.
/// </summary>
public class ExportService : IExportService
{
    private readonly IExportEngineFactory _engineFactory;
    private readonly ILogger<ExportService> _logger;

    public ExportService(IExportEngineFactory engineFactory, ILogger<ExportService> logger)
    {
        _engineFactory = engineFactory;
        _logger = logger;
    }

    public async Task<ExportResult> ExportAsync(ExportRequest request, CancellationToken cancellationToken = default)
    {
        DocumentModel document;
        try
        {
            document = DocumentJsonMapper.FromJson(request.Json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Invalid export JSON payload");
            throw new DocumentServiceException("The supplied JSON is not valid.", ex);
        }

        var engine = _engineFactory.GetEngine(request.Format);
        var content = await engine.GenerateAsync(document, cancellationToken);

        var (extension, contentType) = request.Format switch
        {
            ExportFormat.Excel => ("xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"),
            ExportFormat.Csv => ("csv", "text/csv"),
            ExportFormat.Word => ("docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document"),
            ExportFormat.Pdf => ("pdf", "application/pdf"),
            ExportFormat.Markdown => ("md", "text/markdown"),
            _ => throw new DocumentServiceException($"Unsupported export format '{request.Format}'.")
        };

        var fileName = $"{Sanitize(document.Title)}.{extension}";

        _logger.LogInformation("Exported '{Title}' as {Format} ({Bytes} bytes)", document.Title, request.Format, content.Length);

        return new ExportResult
        {
            Content = content,
            FileName = fileName,
            ContentType = contentType
        };
    }

    private static string Sanitize(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return "document";
        var invalid = Path.GetInvalidFileNameChars();
        return new string(title.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
    }
}
