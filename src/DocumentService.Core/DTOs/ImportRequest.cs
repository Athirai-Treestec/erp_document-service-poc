using DocumentService.Core.Enums;

namespace DocumentService.Core.DTOs;

/// <summary>
/// Input to IImportService: the raw uploaded file bytes plus which format to parse it as.
/// </summary>
public class ImportRequest
{
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public ImportFormat Format { get; set; }
    public string? FileName { get; set; }
}
