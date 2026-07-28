using DocumentService.Core.Enums;

namespace DocumentService.Core.Interfaces;

public interface IPrintRendererFactory
{
    IPrintRenderer GetRenderer(PrintOutputFormat format);
}
