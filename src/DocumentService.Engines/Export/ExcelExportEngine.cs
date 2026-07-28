using ClosedXML.Excel;
using DocumentService.Core.Enums;
using DocumentService.Core.Exceptions;
using DocumentService.Core.Interfaces;
using DocumentService.Core.Models;
using Microsoft.Extensions.Logging;

namespace DocumentService.Engines.Export;

/// <summary>
/// Writes DocumentModel as an .xlsx workbook using ClosedXML.
/// </summary>
public class ExcelExportEngine : IExportEngine
{
    private readonly ILogger<ExcelExportEngine> _logger;

    public ExcelExportEngine(ILogger<ExcelExportEngine> logger)
    {
        _logger = logger;
    }

    public ExportFormat Format => ExportFormat.Excel;

    public Task<byte[]> GenerateAsync(DocumentModel document, CancellationToken cancellationToken = default)
    {
        try
        {
            using var workbook = new XLWorkbook();
            var sheetName = string.IsNullOrWhiteSpace(document.Title) ? "Sheet1" : Sanitize(document.Title);
            var worksheet = workbook.Worksheets.Add(sheetName);

            var startRow = 1;

            if (document.Options.IncludeHeaderRow)
            {
                for (var i = 0; i < document.Columns.Count; i++)
                {
                    var cell = worksheet.Cell(startRow, i + 1);
                    cell.Value = document.Columns[i].Header;
                    cell.Style.Font.Bold = true;
                }
                startRow++;
            }

            for (var r = 0; r < document.Rows.Count; r++)
            {
                var row = document.Rows[r];
                for (var c = 0; c < document.Columns.Count; c++)
                {
                    row.TryGetValue(document.Columns[c].Field, out var value);
                    SetCellValue(worksheet.Cell(startRow + r, c + 1), value);
                }
            }

            if (document.Options.AutoFitColumns)
            {
                worksheet.Columns().AdjustToContents();
            }

            using var memoryStream = new MemoryStream();
            workbook.SaveAs(memoryStream);
            return Task.FromResult(memoryStream.ToArray());
        }
        catch (Exception ex) when (ex is not DocumentServiceException)
        {
            _logger.LogError(ex, "Excel export failed for document '{Title}'", document.Title);
            throw new DocumentServiceException("Failed to generate Excel document.", ex);
        }
    }

    private static void SetCellValue(IXLCell cell, object? value)
    {
        switch (value)
        {
            case null:
                cell.Clear();
                break;
            case long l:
                cell.Value = l;
                break;
            case double d:
                cell.Value = d;
                break;
            case bool b:
                cell.Value = b;
                break;
            case DateTime dt:
                cell.Value = dt;
                break;
            default:
                cell.Value = value.ToString();
                break;
        }
    }

    private static string Sanitize(string sheetName)
    {
        var invalid = new[] { '\\', '/', '*', '?', ':', '[', ']' };
        var clean = sheetName;
        foreach (var ch in invalid)
        {
            clean = clean.Replace(ch, ' ');
        }
        return clean.Length > 31 ? clean[..31] : clean;
    }
}
