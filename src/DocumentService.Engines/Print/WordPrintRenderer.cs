using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DocumentService.Core.Enums;
using DocumentService.Core.Exceptions;
using DocumentService.Core.Interfaces;
using DocumentService.Engines.Print.Internal;
using Microsoft.Extensions.Logging;

namespace DocumentService.Engines.Print;

/// <summary>
/// Renders a merged print template into a .docx using the Open XML SDK,
/// sharing the same block model as PdfPrintRenderer so both stay in sync.
/// </summary>
public class WordPrintRenderer : IPrintRenderer
{
    private readonly ILogger<WordPrintRenderer> _logger;

    public WordPrintRenderer(ILogger<WordPrintRenderer> logger)
    {
        _logger = logger;
    }

    public PrintOutputFormat Format => PrintOutputFormat.Word;

    public Task<byte[]> RenderAsync(string html, CancellationToken cancellationToken = default)
    {
        try
        {
            var blocks = SimpleHtmlParser.Parse(html);

            using var memoryStream = new MemoryStream();
            using (var wordDocument = WordprocessingDocument.Create(memoryStream, WordprocessingDocumentType.Document, true))
            {
                var mainPart = wordDocument.AddMainDocumentPart();
                mainPart.Document = new Document();
                var body = mainPart.Document.AppendChild(new Body());

                foreach (var block in blocks)
                {
                    switch (block)
                    {
                        case HeadingBlock heading:
                            body.AppendChild(new Paragraph(new Run(new RunProperties(new Bold(), new FontSize { Val = "32" }), new Text(heading.Text))));
                            break;

                        case ParagraphBlock paragraph:
                            body.AppendChild(new Paragraph(new Run(new Text(paragraph.Text))));
                            break;

                        case TableBlock table when table.Rows.Count > 0:
                            body.AppendChild(BuildTable(table));
                            break;
                    }
                }

                mainPart.Document.Save();
            }

            return Task.FromResult(memoryStream.ToArray());
        }
        catch (Exception ex) when (ex is not DocumentServiceException)
        {
            _logger.LogError(ex, "Word print rendering failed");
            throw new DocumentServiceException("Failed to render Word document.", ex);
        }
    }

    private static Table BuildTable(TableBlock tableBlock)
    {
        var table = new Table();
        table.AppendChild(new TableProperties(
            new TableBorders(
                new TopBorder { Val = BorderValues.Single, Size = 6 },
                new BottomBorder { Val = BorderValues.Single, Size = 6 },
                new LeftBorder { Val = BorderValues.Single, Size = 6 },
                new RightBorder { Val = BorderValues.Single, Size = 6 },
                new InsideHorizontalBorder { Val = BorderValues.Single, Size = 6 },
                new InsideVerticalBorder { Val = BorderValues.Single, Size = 6 })));

        for (var r = 0; r < tableBlock.Rows.Count; r++)
        {
            var isHeaderRow = r == 0 && tableBlock.FirstRowIsHeader;
            var tableRow = new TableRow();
            foreach (var cellText in tableBlock.Rows[r])
            {
                var run = new Run(new Text(cellText) { Space = SpaceProcessingModeValues.Preserve });
                if (isHeaderRow)
                {
                    run.RunProperties = new RunProperties(new Bold());
                }
                tableRow.AppendChild(new TableCell(new Paragraph(run)));
            }
            table.AppendChild(tableRow);
        }

        return table;
    }
}
