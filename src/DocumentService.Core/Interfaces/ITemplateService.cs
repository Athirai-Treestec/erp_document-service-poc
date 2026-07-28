namespace DocumentService.Core.Interfaces;

/// <summary>
/// Resolves a template name (e.g. "SalesInvoice") to its HTML content and merges
/// data placeholders into it. For the POC this reads from the templates/ folder;
/// swapping in the Certificate Designer engine later only means replacing this
/// implementation.
/// </summary>
public interface ITemplateService
{
    Task<string> RenderAsync(string templateName, IReadOnlyDictionary<string, object?> data, CancellationToken cancellationToken = default);
}
