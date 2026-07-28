using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DocumentService.Core.Enums;
using DocumentService.Core.Exceptions;
using DocumentService.Core.Interfaces;
using DocumentService.Core.Models;
using Microsoft.Extensions.Logging;

namespace DocumentService.Engines.Export;

/// <summary>
/// Writes DocumentModel as a .docx file (title + table) using the Open XML SDK.
/// </summary>
public class WordExportEngine : IExportEngine
{
    private readonly ILogger<WordExportEngine> _logger;

    public WordExportEngine(ILogger<WordExportEngine> logger)
    {
        _logger = logger;
    }

    public ExportFormat Format => ExportFormat.Word;

    public Task<byte[]> GenerateAsync(DocumentModel document, CancellationToken cancellationToken = default)
    {
        try
        {
            using var memoryStream = new MemoryStream();
            using (var wordDocument = WordprocessingDocument.Create(memoryStream, WordprocessingDocumentType.Document, true))
            {
                var mainPart = wordDocument.AddMainDocumentPart();
                mainPart.Document = new Document();
                var body = mainPart.Document.AppendChild(new Body());

                if (!string.IsNullOrWhiteSpace(document.Title))
                {
                    body.AppendChild(CreateTitleParagraph(document.Title));
                }

                if (document.Columns.Count > 0)
                {
                    body.AppendChild(BuildTable(document));
                }

                mainPart.Document.Save();
            }

            return Task.FromResult(memoryStream.ToArray());
        }
        catch (Exception ex) when (ex is not DocumentServiceException)
        {
            _logger.LogError(ex, "Word export failed for document '{Title}'", document.Title);
            throw new DocumentServiceException("Failed to generate Word document.", ex);
        }
    }

    private static Paragraph CreateTitleParagraph(string title)
    {
        var run = new Run(new Text(title));
        run.RunProperties = new RunProperties(new Bold(), new FontSize { Val = "32" });
        return new Paragraph(run);
    }

    private static Table BuildTable(DocumentModel document)
    {
        var table = new Table();

        var tableProperties = new TableProperties(
            new TableBorders(
                new TopBorder { Val = BorderValues.Single, Size = 6 },
                new BottomBorder { Val = BorderValues.Single, Size = 6 },
                new LeftBorder { Val = BorderValues.Single, Size = 6 },
                new RightBorder { Val = BorderValues.Single, Size = 6 },
                new InsideHorizontalBorder { Val = BorderValues.Single, Size = 6 },
                new InsideVerticalBorder { Val = BorderValues.Single, Size = 6 }));
        table.AppendChild(tableProperties);

        if (document.Options.IncludeHeaderRow)
        {
            var headerRow = new TableRow();
            foreach (var column in document.Columns)
            {
                headerRow.AppendChild(CreateCell(column.Header, bold: true));
            }
            table.AppendChild(headerRow);
        }

        foreach (var row in document.Rows)
        {
            var tableRow = new TableRow();
            foreach (var column in document.Columns)
            {
                row.TryGetValue(column.Field, out var value);
                tableRow.AppendChild(CreateCell(value?.ToString() ?? string.Empty));
            }
            table.AppendChild(tableRow);
        }

        return table;
    }

    private static TableCell CreateCell(string text, bool bold = false)
    {
        var run = new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
        if (bold)
        {
            run.RunProperties = new RunProperties(new Bold());
        }
        return new TableCell(new Paragraph(run));
    }
}
