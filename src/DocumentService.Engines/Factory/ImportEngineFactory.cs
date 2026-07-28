using DocumentService.Core.Enums;
using DocumentService.Core.Exceptions;
using DocumentService.Core.Interfaces;

namespace DocumentService.Engines.Factory;

/// <summary>
/// Selects the IImportEngine strategy for a given ImportFormat.
/// </summary>
public class ImportEngineFactory : IImportEngineFactory
{
    private readonly IReadOnlyDictionary<ImportFormat, IImportEngine> _engines;

    public ImportEngineFactory(IEnumerable<IImportEngine> engines)
    {
        _engines = engines.ToDictionary(e => e.Format);
    }

    public IImportEngine GetEngine(ImportFormat format)
    {
        if (_engines.TryGetValue(format, out var engine))
        {
            return engine;
        }

        throw new DocumentServiceException($"No import engine registered for format '{format}'.");
    }
}
