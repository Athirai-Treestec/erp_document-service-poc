using DocumentService.Core.Enums;
using DocumentService.Core.Exceptions;
using DocumentService.Core.Interfaces;

namespace DocumentService.Engines.Print;

/// <summary>
/// Selects the IPrintRenderer strategy for a given PrintOutputFormat.
/// </summary>
public class PrintRendererFactory : IPrintRendererFactory
{
    private readonly IReadOnlyDictionary<PrintOutputFormat, IPrintRenderer> _renderers;

    public PrintRendererFactory(IEnumerable<IPrintRenderer> renderers)
    {
        _renderers = renderers.ToDictionary(r => r.Format);
    }

    public IPrintRenderer GetRenderer(PrintOutputFormat format)
    {
        if (_renderers.TryGetValue(format, out var renderer))
        {
            return renderer;
        }

        throw new DocumentServiceException($"No print renderer registered for format '{format}'.");
    }
}
