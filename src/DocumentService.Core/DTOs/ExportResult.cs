namespace DocumentService.Core.DTOs;

/// <summary>
/// Output of an export: the generated file bytes plus enough metadata
/// for the caller to serve it as a download (e.g. via ASP.NET File()).
/// </summary>
public class ExportResult
{
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
}
