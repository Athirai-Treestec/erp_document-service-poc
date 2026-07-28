using ClosedXML.Excel;
using DocumentService.Core.Enums;
using DocumentService.Core.Exceptions;
using DocumentService.Core.Interfaces;
using DocumentService.Core.Models;
using Microsoft.Extensions.Logging;

namespace DocumentService.Engines.Import;

/// <summary>
/// Reads the first worksheet of an .xlsx file using ClosedXML. The first row is
/// treated as the header; header text is used as both Field key and display Header
/// since there is no separate "business field name" available in a raw spreadsheet.
/// </summary>
public class ExcelImportEngine : IImportEngine
{
    private readonly ILogger<ExcelImportEngine> _logger;

    public ExcelImportEngine(ILogger<ExcelImportEngine> logger)
    {
        _logger = logger;
    }

    public ImportFormat Format => ImportFormat.Excel;

    public Task<DocumentModel> ReadAsync(Stream fileStream, CancellationToken cancellationToken = default)
    {
        try
        {
            using var workbook = new XLWorkbook(fileStream);
            var worksheet = workbook.Worksheets.First();
            var usedRange = worksheet.RangeUsed();

            var model = new DocumentModel { Title = worksheet.Name };

            if (usedRange is null)
            {
                return Task.FromResult(model);
            }

            var rows = usedRange.RowsUsed().ToList();
            if (rows.Count == 0)
            {
                return Task.FromResult(model);
            }

            var headerRow = rows[0];
            var columnCount = headerRow.CellsUsed().Count();

            for (var c = 1; c <= columnCount; c++)
            {
                var header = headerRow.Cell(c).GetString();
                model.Columns.Add(new DocumentColumn { Header = header, Field = header });
            }

            for (var r = 1; r < rows.Count; r++)
            {
                var dataRow = rows[r];
                var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                for (var c = 1; c <= columnCount; c++)
                {
                    var cell = dataRow.Cell(c);
                    dict[model.Columns[c - 1].Field] = GetCellValue(cell);
                }
                model.Rows.Add(dict);
            }

            return Task.FromResult(model);
        }
        catch (Exception ex) when (ex is not DocumentServiceException)
        {
            _logger.LogError(ex, "Excel import failed");
            throw new DocumentServiceException("Failed to read Excel file.", ex);
        }
    }

    private static object? GetCellValue(IXLCell cell)
    {
        return cell.DataType switch
        {
            XLDataType.Number => cell.GetDouble(),
            XLDataType.Boolean => cell.GetBoolean(),
            XLDataType.DateTime => cell.GetDateTime(),
            XLDataType.Blank => null,
            _ => cell.GetString()
        };
    }
}
