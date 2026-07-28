using System.Globalization;
using CsvHelper;
using DocumentService.Core.Enums;
using DocumentService.Core.Exceptions;
using DocumentService.Core.Interfaces;
using DocumentService.Core.Models;
using Microsoft.Extensions.Logging;

namespace DocumentService.Engines.Import;

/// <summary>
/// Reads a CSV file using CsvHelper, treating the first row as the header.
/// </summary>
public class CsvImportEngine : IImportEngine
{
    private readonly ILogger<CsvImportEngine> _logger;

    public CsvImportEngine(ILogger<CsvImportEngine> logger)
    {
        _logger = logger;
    }

    public ImportFormat Format => ImportFormat.Csv;

    public async Task<DocumentModel> ReadAsync(Stream fileStream, CancellationToken cancellationToken = default)
    {
        try
        {
            var model = new DocumentModel { Title = "Imported CSV" };

            using var reader = new StreamReader(fileStream);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

            if (!await csv.ReadAsync() || !csv.ReadHeader())
            {
                return model;
            }

            var headers = csv.HeaderRecord ?? Array.Empty<string>();
            foreach (var header in headers)
            {
                model.Columns.Add(new DocumentColumn { Header = header, Field = header });
            }

            while (await csv.ReadAsync())
            {
                var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (var header in headers)
                {
                    dict[header] = ParseValue(csv.GetField(header));
                }
                model.Rows.Add(dict);
            }

            return model;
        }
        catch (Exception ex) when (ex is not DocumentServiceException)
        {
            _logger.LogError(ex, "CSV import failed");
            throw new DocumentServiceException("Failed to read CSV file.", ex);
        }
    }

    private static object? ParseValue(string? raw)
    {
        if (string.IsNullOrEmpty(raw)) return raw;
        if (long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l)) return l;
        if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) return d;
        if (bool.TryParse(raw, out var b)) return b;
        return raw;
    }
}
