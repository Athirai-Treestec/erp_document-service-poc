using DocumentService.Core.Enums;

namespace DocumentService.Core.Interfaces;

public interface IImportEngineFactory
{
    IImportEngine GetEngine(ImportFormat format);
}
