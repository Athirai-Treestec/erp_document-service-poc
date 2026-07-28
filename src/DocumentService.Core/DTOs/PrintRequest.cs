using DocumentService.Core.Enums;

namespace DocumentService.Core.DTOs;

/// <summary>
/// Input to IPrintService: a named template (e.g. "SalesInvoice") plus the
/// JSON data to merge into it, and the desired output format.
/// </summary>
public class PrintRequest
{
    public string TemplateName { get; set; } = string.Empty;
    public string Json { get; set; } = string.Empty;
    public PrintOutputFormat Format { get; set; } = PrintOutputFormat.Pdf;
}
