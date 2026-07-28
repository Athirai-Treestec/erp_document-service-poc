using DocumentService.Core.Enums;

namespace DocumentService.Core.Interfaces;

public interface IExportEngineFactory
{
    IExportEngine GetEngine(ExportFormat format);
}
