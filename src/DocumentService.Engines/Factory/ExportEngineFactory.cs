using DocumentService.Core.Enums;
using DocumentService.Core.Exceptions;
using DocumentService.Core.Interfaces;

namespace DocumentService.Engines.Factory;

/// <summary>
/// Selects the IExportEngine strategy for a given ExportFormat.
/// Adding a new format is: implement IExportEngine, register it in DI — nothing here changes.
/// </summary>
public class ExportEngineFactory : IExportEngineFactory
{
    private readonly IReadOnlyDictionary<ExportFormat, IExportEngine> _engines;

    public ExportEngineFactory(IEnumerable<IExportEngine> engines)
    {
        _engines = engines.ToDictionary(e => e.Format);
    }

    public IExportEngine GetEngine(ExportFormat format)
    {
        if (_engines.TryGetValue(format, out var engine))
        {
            return engine;
        }

        throw new DocumentServiceException($"No export engine registered for format '{format}'.");
    }
}
