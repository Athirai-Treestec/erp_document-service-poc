using DocumentService.Core.DTOs;
using DocumentService.Core.Enums;
using DocumentService.Core.Exceptions;
using DocumentService.Core.Interfaces;
using DocumentService.Core.Models;
using Microsoft.Extensions.Logging;

namespace DocumentService.Engines.Print;

/// <summary>
/// Orchestrates the Print pipeline: merge JSON into the named template, then hand
/// the merged HTML to the renderer strategy selected for the requested output format.
/// </summary>
public class PrintService : IPrintService
{
    private readonly ITemplateService _templateService;
    private readonly IPrintRendererFactory _rendererFactory;
    private readonly ILogger<PrintService> _logger;

    public PrintService(ITemplateService templateService, IPrintRendererFactory rendererFactory, ILogger<PrintService> logger)
    {
        _templateService = templateService;
        _rendererFactory = rendererFactory;
        _logger = logger;
    }

    public async Task<PrintResult> PrintAsync(PrintRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.TemplateName))
        {
            throw new DocumentServiceException("TemplateName is required.");
        }

        Dictionary<string, object?> data;
        try
        {
            data = JsonValueConverter.ParseObject(request.Json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Invalid print JSON payload for template '{TemplateName}'", request.TemplateName);
            throw new DocumentServiceException("The supplied JSON is not valid.", ex);
        }

        var mergedHtml = await _templateService.RenderAsync(request.TemplateName, data, cancellationToken);
        var renderer = _rendererFactory.GetRenderer(request.Format);
        var content = await renderer.RenderAsync(mergedHtml, cancellationToken);

        var (extension, contentType) = request.Format switch
        {
            PrintOutputFormat.Pdf => ("pdf", "application/pdf"),
            PrintOutputFormat.Word => ("docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document"),
            _ => throw new DocumentServiceException($"Unsupported print output format '{request.Format}'.")
        };

        _logger.LogInformation("Printed template '{TemplateName}' as {Format} ({Bytes} bytes)", request.TemplateName, request.Format, content.Length);

        return new PrintResult
        {
            Content = content,
            FileName = $"{request.TemplateName}.{extension}",
            ContentType = contentType,
            PreviewHtml = mergedHtml
        };
    }
}
