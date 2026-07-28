using DocumentService.Core.Enums;
using DocumentService.Core.Exceptions;
using DocumentService.Core.Interfaces;
using DocumentService.Core.Models;
using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace DocumentService.Engines.Export;

/// <summary>
/// Writes DocumentModel as a tabular PDF using QuestPDF. This engine only knows
/// how to lay out a DocumentModel — it is not the same thing as IPrintRenderer,
/// which renders arbitrary merged HTML templates for the Print pipeline.
/// </summary>
public class PdfExportEngine : IExportEngine
{
    private readonly ILogger<PdfExportEngine> _logger;

    public PdfExportEngine(ILogger<PdfExportEngine> logger)
    {
        _logger = logger;
    }

    public ExportFormat Format => ExportFormat.Pdf;

    public Task<byte[]> GenerateAsync(DocumentModel document, CancellationToken cancellationToken = default)
    {
        try
        {
            var pdfDocument = QuestPDF.Fluent.Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(30);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    page.Header().Text(document.Title).FontSize(18).Bold();

                    page.Content().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            foreach (var _ in document.Columns)
                            {
                                columns.RelativeColumn();
                            }
                        });

                        if (document.Options.IncludeHeaderRow)
                        {
                            table.Header(header =>
                            {
                                foreach (var column in document.Columns)
                                {
                                    header.Cell().Background(Colors.Grey.Lighten2).Padding(4).Text(column.Header).Bold();
                                }
                            });
                        }

                        foreach (var row in document.Rows)
                        {
                            foreach (var column in document.Columns)
                            {
                                row.TryGetValue(column.Field, out var value);
                                table.Cell().Padding(4).Text(value?.ToString() ?? string.Empty);
                            }
                        }
                    });

                    page.Footer().AlignCenter().Text(text =>
                    {
                        text.CurrentPageNumber();
                        text.Span(" / ");
                        text.TotalPages();
                    });
                });
            });

            return Task.FromResult(pdfDocument.GeneratePdf());
        }
        catch (Exception ex) when (ex is not DocumentServiceException)
        {
            _logger.LogError(ex, "PDF export failed for document '{Title}'", document.Title);
            throw new DocumentServiceException("Failed to generate PDF document.", ex);
        }
    }
}
