using System.Text;
using DocumentService.Core.Enums;
using DocumentService.Core.Exceptions;
using DocumentService.Core.Interfaces;
using DocumentService.Core.Models;
using Microsoft.Extensions.Logging;

namespace DocumentService.Engines.Export;

/// <summary>
/// Writes DocumentModel as a Markdown table. No third-party library needed —
/// Markdown tables are simple enough to build with StringBuilder directly.
/// </summary>
public class MarkdownExportEngine : IExportEngine
{
    private readonly ILogger<MarkdownExportEngine> _logger;

    public MarkdownExportEngine(ILogger<MarkdownExportEngine> logger)
    {
        _logger = logger;
    }

    public ExportFormat Format => ExportFormat.Markdown;

    public Task<byte[]> GenerateAsync(DocumentModel document, CancellationToken cancellationToken = default)
    {
        try
        {
            var sb = new StringBuilder();

            if (!string.IsNullOrWhiteSpace(document.Title))
            {
                sb.AppendLine($"# {document.Title}");
                sb.AppendLine();
            }

            if (document.Columns.Count > 0)
            {
                sb.AppendLine("| " + string.Join(" | ", document.Columns.Select(c => Escape(c.Header))) + " |");
                sb.AppendLine("| " + string.Join(" | ", document.Columns.Select(_ => "---")) + " |");

                foreach (var row in document.Rows)
                {
                    var cells = document.Columns.Select(c =>
                    {
                        row.TryGetValue(c.Field, out var value);
                        return Escape(value?.ToString() ?? string.Empty);
                    });
                    sb.AppendLine("| " + string.Join(" | ", cells) + " |");
                }
            }

            return Task.FromResult(Encoding.UTF8.GetBytes(sb.ToString()));
        }
        catch (Exception ex) when (ex is not DocumentServiceException)
        {
            _logger.LogError(ex, "Markdown export failed for document '{Title}'", document.Title);
            throw new DocumentServiceException("Failed to generate Markdown document.", ex);
        }
    }

    private static string Escape(string text) => text.Replace("|", "\\|");
}
