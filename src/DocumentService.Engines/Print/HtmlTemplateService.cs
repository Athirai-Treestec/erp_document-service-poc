using System.Text;
using System.Text.RegularExpressions;
using DocumentService.Core.Exceptions;
using DocumentService.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace DocumentService.Engines.Print;

/// <summary>
/// Loads a named HTML template from disk and merges placeholder data into it.
/// Supports simple placeholders ({{Field}}) and one level of repeating blocks
/// ({{#each Items}}...{{/each}}) for line-item tables. This is intentionally a
/// minimal templating engine for the POC — swapping in the Certificate Designer
/// engine later means replacing only this class, since ITemplateService is the
/// only contract PrintService depends on.
/// </summary>
public class HtmlTemplateService : ITemplateService
{
    private static readonly Regex EachBlockRegex = new(@"\{\{#each\s+(\w+)\}\}(.*?)\{\{/each\}\}", RegexOptions.Singleline);
    private static readonly Regex PlaceholderRegex = new(@"\{\{(\w+)\}\}");

    private readonly PrintServiceOptions _options;
    private readonly ILogger<HtmlTemplateService> _logger;

    public HtmlTemplateService(PrintServiceOptions options, ILogger<HtmlTemplateService> logger)
    {
        _options = options;
        _logger = logger;
    }

    public async Task<string> RenderAsync(string templateName, IReadOnlyDictionary<string, object?> data, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(_options.TemplatesDirectory, $"{templateName}.html");

        if (!File.Exists(path))
        {
            _logger.LogError("Template '{TemplateName}' not found at {Path}", templateName, path);
            throw new DocumentServiceException($"Template '{templateName}' was not found.");
        }

        try
        {
            var template = await File.ReadAllTextAsync(path, cancellationToken);

            var withLoopsExpanded = EachBlockRegex.Replace(template, match =>
            {
                var arrayField = match.Groups[1].Value;
                var innerTemplate = match.Groups[2].Value;

                if (!data.TryGetValue(arrayField, out var arrayValue) || arrayValue is not IEnumerable<object?> items)
                {
                    return string.Empty;
                }

                var sb = new StringBuilder();
                foreach (var item in items)
                {
                    var itemData = item as IReadOnlyDictionary<string, object?> ?? new Dictionary<string, object?>();
                    sb.Append(PlaceholderRegex.Replace(innerTemplate, m => Resolve(itemData, m.Groups[1].Value)));
                }
                return sb.ToString();
            });

            return PlaceholderRegex.Replace(withLoopsExpanded, m => Resolve(data, m.Groups[1].Value));
        }
        catch (Exception ex) when (ex is not DocumentServiceException)
        {
            _logger.LogError(ex, "Failed to render template '{TemplateName}'", templateName);
            throw new DocumentServiceException($"Failed to render template '{templateName}'.", ex);
        }
    }

    private static string Resolve(IReadOnlyDictionary<string, object?> data, string key) =>
        data.TryGetValue(key, out var value) ? value?.ToString() ?? string.Empty : string.Empty;
}
