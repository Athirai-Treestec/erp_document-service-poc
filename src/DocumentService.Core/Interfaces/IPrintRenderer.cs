using DocumentService.Core.Enums;

namespace DocumentService.Core.Interfaces;

/// <summary>
/// Renders a fully-merged HTML string into a final output format. One implementation
/// per PrintOutputFormat (mirrors IExportEngine), so the underlying renderer — e.g.
/// QuestPDF today, a future "Render-PDF" service or the Certificate Designer engine —
/// can be replaced without changing PrintService's logic.
/// </summary>
public interface IPrintRenderer
{
    PrintOutputFormat Format { get; }
    Task<byte[]> RenderAsync(string html, CancellationToken cancellationToken = default);
}
