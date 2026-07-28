namespace DocumentService.Core.Models;

/// <summary>
/// Describes a single column: the display label (Header) and the key used
/// to look up the value in each row dictionary (Field).
/// </summary>
public class DocumentColumn
{
    public string Header { get; set; } = string.Empty;
    public string Field { get; set; } = string.Empty;
}
