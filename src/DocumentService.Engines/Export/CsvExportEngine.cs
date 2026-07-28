using System.Globalization;
using CsvHelper;
using DocumentService.Core.Enums;
using DocumentService.Core.Exceptions;
using DocumentService.Core.Interfaces;
using DocumentService.Core.Models;
using Microsoft.Extensions.Logging;

namespace DocumentService.Engines.Export;

/// <summary>
/// Writes DocumentModel as CSV using CsvHelper, which handles quoting/escaping
/// (commas, quotes, newlines in values) correctly rather than doing it by hand.
/// </summary>
public class CsvExportEngine : IExportEngine
{
    private readonly ILogger<CsvExportEngine> _logger;

    public CsvExportEngine(ILogger<CsvExportEngine> logger)
    {
        _logger = logger;
    }

    public ExportFormat Format => ExportFormat.Csv;

    public async Task<byte[]> GenerateAsync(DocumentModel document, CancellationToken cancellationToken = default)
    {
        try
        {
            using var memoryStream = new MemoryStream();
            await using (var writer = new StreamWriter(memoryStream, leaveOpen: true))
            await using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
            {
                if (document.Options.IncludeHeaderRow)
                {
                    foreach (var column in document.Columns)
                    {
                        csv.WriteField(column.Header);
                    }
                    await csv.NextRecordAsync();
                }

                foreach (var row in document.Rows)
                {
                    foreach (var column in document.Columns)
                    {
                        row.TryGetValue(column.Field, out var value);
                        csv.WriteField(value);
                    }
                    await csv.NextRecordAsync();
                }
            }

            return memoryStream.ToArray();
        }
        catch (Exception ex) when (ex is not DocumentServiceException)
        {
            _logger.LogError(ex, "CSV export failed for document '{Title}'", document.Title);
            throw new DocumentServiceException("Failed to generate CSV document.", ex);
        }
    }
}
