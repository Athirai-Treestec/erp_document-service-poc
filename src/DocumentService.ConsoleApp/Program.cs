using DocumentService.Core.DTOs;
using DocumentService.Core.Enums;
using DocumentService.Core.Interfaces;
using DocumentService.Engines.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var services = new ServiceCollection();
services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
services.AddDocumentService();

await using var provider = services.BuildServiceProvider();
var documentService = provider.GetRequiredService<IDocumentService>();

var baseDir = AppContext.BaseDirectory;
var samplesDir = Path.Combine(baseDir, "samples");
var outputDir = Path.Combine(baseDir, "output");
Directory.CreateDirectory(outputDir);

Console.WriteLine("=== 1. EXPORT: Sales Invoice Report -> Excel / CSV / Word / PDF / Markdown ===");
var exportJson = await File.ReadAllTextAsync(Path.Combine(samplesDir, "print-quotation1.json"));

string? excelPath = null;
string? csvPath = null;

//foreach (var format in Enum.GetValues<ExportFormat>())
//{
//    var result = await documentService.ExportAsync(new ExportRequest { Json = exportJson, Format = format });
//    var path = Path.Combine(outputDir, result.FileName);
//    await File.WriteAllBytesAsync(path, result.Content);
//    Console.WriteLine($"  [{format,-9}] -> {path} ({result.Content.Length} bytes)");

//    if (format == ExportFormat.Excel) excelPath = path;
//    if (format == ExportFormat.Csv) csvPath = path;
//}

//Console.WriteLine();
//Console.WriteLine("=== 2. IMPORT: read the generated Excel/CSV back into common JSON ===");

//var excelBytes = await File.ReadAllBytesAsync(excelPath!);
//var excelImportResult = await documentService.ImportAsync(new ImportRequest { Content = excelBytes, Format = ImportFormat.Excel, FileName = "SalesInvoiceReport.xlsx" });
//await File.WriteAllTextAsync(Path.Combine(outputDir, "import-from-excel.json"), excelImportResult.Json);
//Console.WriteLine("  Excel -> JSON:");
//Console.WriteLine(Indent(excelImportResult.Json));

//var csvBytes = await File.ReadAllBytesAsync(csvPath!);
//var csvImportResult = await documentService.ImportAsync(new ImportRequest { Content = csvBytes, Format = ImportFormat.Csv, FileName = "SalesInvoiceReport.csv" });
//await File.WriteAllTextAsync(Path.Combine(outputDir, "import-from-csv.json"), csvImportResult.Json);
//Console.WriteLine("  CSV -> JSON:");
//Console.WriteLine(Indent(csvImportResult.Json));

Console.WriteLine();
Console.WriteLine("=== 3. PRINT: Templates -> PDF (and one Word sample) ===");

var printJobs = new (string Template, string SampleFile)[]
{
    //("SalesInvoice", "print-sales-invoice.json"),
    //("Quotation1", "print-quotation1.json"),
    ("Receipt", "export-sample.json"),
    //("Receipt", "print-receipt.json")
};

foreach (var (template, sampleFile) in printJobs)
{
    var json = await File.ReadAllTextAsync(Path.Combine(samplesDir, sampleFile));
    var pdfResult = await documentService.PrintAsync(new PrintRequest { TemplateName = template, Json = json, Format = PrintOutputFormat.Pdf });
    var pdfPath = Path.Combine(outputDir, pdfResult.FileName);
    await File.WriteAllBytesAsync(pdfPath, pdfResult.Content);
    Console.WriteLine($"  [{template,-12}] PDF  -> {pdfPath} ({pdfResult.Content.Length} bytes)");
}

// Also demonstrate the Word renderer once, reusing the same template + data.
var invoiceJson = await File.ReadAllTextAsync(Path.Combine(samplesDir, "print-sales-invoice.json"));
var wordResult = await documentService.PrintAsync(new PrintRequest { TemplateName = "SalesInvoice", Json = invoiceJson, Format = PrintOutputFormat.Word });
var wordPath = Path.Combine(outputDir, wordResult.FileName);
await File.WriteAllBytesAsync(wordPath, wordResult.Content);
Console.WriteLine($"  [SalesInvoice ] Word -> {wordPath} ({wordResult.Content.Length} bytes)");

Console.WriteLine();
Console.WriteLine($"Done. All generated files are in: {outputDir}");

static string Indent(string text) => string.Join(Environment.NewLine, text.Split(Environment.NewLine).Select(l => "    " + l));
