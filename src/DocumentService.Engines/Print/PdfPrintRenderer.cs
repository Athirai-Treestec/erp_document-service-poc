using DocumentService.Core.Enums;
using DocumentService.Core.Exceptions;
using DocumentService.Core.Interfaces;
using DocumentService.Engines.Print.Internal;
using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace DocumentService.Engines.Print;

/// <summary>
/// Renders a merged print template into a PDF using QuestPDF. Stands in for the
/// "existing Render-PDF renderer" mentioned in the requirements; swapping that
/// renderer in later only means replacing this class behind IPrintRenderer.
/// </summary>
public class PdfPrintRenderer : IPrintRenderer
{
    private readonly ILogger<PdfPrintRenderer> _logger;

    public PdfPrintRenderer(ILogger<PdfPrintRenderer> logger)
    {
        _logger = logger;
    }

    public PrintOutputFormat Format => PrintOutputFormat.Pdf;

    public Task<byte[]> RenderAsync(string html, CancellationToken cancellationToken = default)
    {
        try
        {
            var blocks = SimpleHtmlParser.Parse(html);

            var pdfDocument = QuestPDF.Fluent.Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(30);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    page.Content().Column(column =>
                    {
                        column.Spacing(6);

                        foreach (var block in blocks)
                        {
                            switch (block)
                            {
                                case HeadingBlock heading:
                                    column.Item().Text(heading.Text).FontSize(18).Bold();
                                    break;

                                case ParagraphBlock paragraph:
                                    column.Item().Text(paragraph.Text);
                                    break;

                                case TableBlock table when table.Rows.Count > 0:
                                    column.Item().Table(t =>
                                    {
                                        var columnCount = table.Rows[0].Count;
                                        t.ColumnsDefinition(cols =>
                                        {
                                            for (var i = 0; i < columnCount; i++) cols.RelativeColumn();
                                        });

                                        var startIndex = 0;
                                        if (table.FirstRowIsHeader)
                                        {
                                            t.Header(header =>
                                            {
                                                foreach (var cell in table.Rows[0])
                                                {
                                                    header.Cell().Background(Colors.Grey.Lighten2).Padding(4).Text(cell).Bold();
                                                }
                                            });
                                            startIndex = 1;
                                        }

                                        for (var r = startIndex; r < table.Rows.Count; r++)
                                        {
                                            foreach (var cell in table.Rows[r])
                                            {
                                                t.Cell().Padding(4).Text(cell);
                                            }
                                        }
                                    });
                                    break;
                            }
                        }
                    });
                });
            });

            return Task.FromResult(pdfDocument.GeneratePdf());
        }
        catch (Exception ex) when (ex is not DocumentServiceException)
        {
            _logger.LogError(ex, "PDF print rendering failed");
            throw new DocumentServiceException("Failed to render PDF document.", ex);
        }
    }
}
