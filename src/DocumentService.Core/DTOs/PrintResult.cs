namespace DocumentService.Core.DTOs;

/// <summary>
/// Output of a print/render operation: the generated file bytes and, for the
/// POC's optional preview feature, the raw merged HTML before rendering.
/// </summary>
public class PrintResult
{
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public string? PreviewHtml { get; set; }
}
