namespace DocumentService.Core.Models;

/// <summary>
/// Rendering hints an engine may honor. Kept intentionally small for the POC;
/// new flags (e.g. IncludeLogo, PageOrientation) can be added without touching engine contracts.
/// </summary>
public class DocumentOptions
{
    public bool IncludeHeaderRow { get; set; } = true;
    public bool AutoFitColumns { get; set; } = true;
}
