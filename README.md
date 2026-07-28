# Common Document Service — POC

A small, dependency-injected .NET 10 library that gives ERP modules one
place to **Export**, **Import**, and **Print** documents — without ever
knowing which third-party library does the actual work.

This is a Proof of Concept. It validates the architecture and approach
before it gets wired into the real ERP. It is deliberately **not**
production-hardened: no persistence, no auth, no HTML designer, no retry
policies. Just the shape of the service and proof that it works.

---

## 1. The problem this solves

Today, if five ERP modules each need to export a report to Excel, each one
picks its own library, writes its own formatting code, and re-solves the
same problem five times. If the library ever needs replacing (license,
performance, a bug), every module has to change.

The Document Service inverts that: ERP modules depend on **one interface**
(`IDocumentService`) and a **plain JSON shape**. Everything else —
ClosedXML, CsvHelper, Open XML SDK, QuestPDF — is an implementation detail
hidden behind that interface.

---

## 2. Solution layout

```
DocumentService.sln
src/
  DocumentService.Core/          # No third-party deps. Contracts + the shared model.
    Models/                      #   DocumentModel, DocumentColumn, DocumentOptions,
                                  #   DocumentJsonMapper, JsonValueConverter
    DTOs/                        #   ExportRequest/Result, ImportRequest/Result, PrintRequest/Result
    Enums/                       #   ExportFormat, ImportFormat, PrintOutputFormat
    Interfaces/                  #   IDocumentService, IExportService, IImportService, IPrintService,
                                  #   IExportEngine, IImportEngine, IPrintRenderer, ITemplateService,
                                  #   IExportEngineFactory, IImportEngineFactory, IPrintRendererFactory
    Exceptions/                  #   DocumentServiceException

  DocumentService.Engines/       # All third-party library usage lives here, and only here.
    Export/                      #   ExcelExportEngine (ClosedXML), CsvExportEngine (CsvHelper),
                                  #   WordExportEngine (Open XML SDK), PdfExportEngine (QuestPDF),
                                  #   MarkdownExportEngine (StringBuilder)
    Import/                      #   ExcelImportEngine (ClosedXML), CsvImportEngine (CsvHelper)
    Print/                       #   HtmlTemplateService, PdfPrintRenderer, WordPrintRenderer,
                                  #   PrintRendererFactory, PrintService, Internal/SimpleHtmlParser
    Factory/                     #   ExportEngineFactory, ImportEngineFactory
    Services/                    #   ExportService, ImportService, DocumentServiceFacade
    DependencyInjection/         #   ServiceCollectionExtensions.AddDocumentService()

  DocumentService.ConsoleApp/    # Test harness — exercises Export, Import and Print end to end.

templates/                       # SalesInvoice.html, Quotation.html, Receipt.html
samples/                         # Sample JSON payloads used by the console app
```

`DocumentService.Core` has **no package references** beyond
`Microsoft.Extensions.Logging.Abstractions`. It defines the vocabulary
(models, DTOs, interfaces) that every other project — including a future
ERP module — talks in. `DocumentService.Engines` is the only project that
references ClosedXML, DocumentFormat.OpenXml, CsvHelper, and QuestPDF. An
ERP module would reference `DocumentService.Core` (to code against
`IDocumentService`) and `DocumentService.Engines` (only at composition
root, to call `AddDocumentService()`) — it never references ClosedXML etc.
directly.

---

## 3. Design decisions

### 3.1 One common model — `DocumentModel`
Every export/import engine reads and writes the exact same shape:

```csharp
class DocumentModel {
    string Title;
    string? Company, Header, Footer;
    List<DocumentColumn> Columns;      // Header (display) + Field (row key)
    List<Dictionary<string, object?>> Rows;
    DocumentOptions Options;           // IncludeHeaderRow, AutoFitColumns
}
```

Because every engine agrees on this shape, none of them need to know about
JSON, HTTP, or each other. `DocumentJsonMapper` is the only place that
converts between the caller's JSON and `DocumentModel` — and it uses a
shared `JsonValueConverter` (also used by `PrintService` for arbitrary
print-template data) so JSON numbers/bools/strings become real CLR types
instead of raw `JsonElement`, which keeps every engine's code simple.

### 3.2 Strategy pattern — one class per format
`IExportEngine`, `IImportEngine`, and `IPrintRenderer` are each implemented
once per format (`ExportFormat`, `ImportFormat`, `PrintOutputFormat`
respectively). Each implementation owns exactly one third-party library.
Replacing ClosedXML with a different Excel library later means rewriting
`ExcelExportEngine` — nothing else in the solution changes.

### 3.3 Factory pattern — format → engine lookup
`ExportEngineFactory`, `ImportEngineFactory`, and `PrintRendererFactory`
each take `IEnumerable<TEngine>` from DI (every engine registers itself as
that interface) and build a `format → engine` dictionary. Adding a new
format is: write the engine class, add one `services.AddSingleton<...>()`
line. No factory `switch` statement to maintain.

### 3.4 Facade — `IDocumentService`
`DocumentServiceFacade` is the only thing an ERP module resolves from DI.
It just forwards to `IExportService` / `IImportService` / `IPrintService`.
This is what makes the "business modules must never reference ClosedXML /
OpenXml / CsvHelper / QuestPDF directly" requirement real: those types
aren't even visible from the facade's signature.

### 3.5 Print pipeline — Template + Renderer, kept separate
Printing has two independent concerns, each replaceable on its own:

- **`ITemplateService`** (`HtmlTemplateService`) resolves a template name
  (e.g. `"SalesInvoice"`) to an HTML file in `templates/` and merges JSON
  data into it using `{{Field}}` placeholders and one level of
  `{{#each Items}}...{{/each}}` repeating blocks. This is intentionally a
  minimal templating engine — swapping in the real Certificate Designer
  engine later means replacing only this class.
- **`IPrintRenderer`** (`PdfPrintRenderer`, `WordPrintRenderer`) turns the
  merged HTML into a final PDF or DOCX. QuestPDF and the Open XML SDK are
  not HTML renderers, so a small internal `SimpleHtmlParser`
  (`Print/Internal/`) reads the merged HTML's `<h1-3>`, `<p>`, and
  `<table>` elements into a tiny block model both renderers lay out. This
  is a known, intentional POC simplification — it is not a general
  HTML-to-PDF engine, only enough to prove the "template → render → file"
  architecture. Replacing it with the existing Render-PDF renderer or a
  real HTML engine only means implementing `IPrintRenderer` again.
- `PrintResult.PreviewHtml` carries the merged HTML back so a caller can
  render an on-screen preview before committing to a PDF/Word render —
  covers the "Preview (optional)" requirement without extra plumbing.

### 3.6 Error handling & logging
Every engine/service catches library-specific exceptions at its boundary,
logs them via `ILogger<T>` (`Microsoft.Extensions.Logging`), and rethrows
as `DocumentServiceException`. Callers of `IDocumentService` only ever need
to catch one exception type, regardless of which engine failed underneath.

### 3.7 Async & DI throughout
Every service/engine method is `async` (even the ones that are
CPU-bound today, like ClosedXML/QuestPDF generation) so the interfaces
don't need to change if a future implementation does real I/O (e.g.
calling an external rendering service). Everything is registered and
resolved through `Microsoft.Extensions.DependencyInjection` via the single
`services.AddDocumentService()` extension method — no static/singleton
service locators anywhere in the engines.

---

## 4. How each pipeline works

### Export
```
JSON  →  DocumentJsonMapper.FromJson  →  DocumentModel
      →  ExportEngineFactory.GetEngine(format)
      →  IExportEngine.GenerateAsync(document)
      →  byte[]  (wrapped in ExportResult: bytes + file name + content type)
```

### Import
```
Uploaded file bytes  →  ImportEngineFactory.GetEngine(format)
                     →  IImportEngine.ReadAsync(stream)
                     →  DocumentModel
                     →  DocumentJsonMapper.ToJson  →  JSON string
```
Nothing is written to a database — the import pipeline is a pure
bytes-in, JSON-out transformation.

### Print
```
Template name + JSON  →  ITemplateService.RenderAsync  →  merged HTML
                      →  PrintRendererFactory.GetRenderer(format)
                      →  IPrintRenderer.RenderAsync(html)
                      →  byte[]  (wrapped in PrintResult, plus PreviewHtml)
```

---

## 5. Running the POC

```bash
dotnet build
dotnet run --project src/DocumentService.ConsoleApp/DocumentService.ConsoleApp.csproj
```

The console app (`Program.cs`):
1. Reads [`samples/export-sample.json`](samples/export-sample.json) and exports it to Excel, CSV, Word, PDF, and Markdown.
2. Reads the just-generated Excel and CSV files back through the Import pipeline and prints the resulting JSON.
3. Renders `SalesInvoice`, `Quotation`, and `Receipt` templates to PDF using the matching sample JSON in `samples/`, plus one Word render of `SalesInvoice`.

All generated files land in `src/DocumentService.ConsoleApp/bin/Debug/net10.0/output/`.

---

## 6. Usage example (what an ERP module would write)

```csharp
var services = new ServiceCollection();
services.AddLogging(b => b.AddConsole());
services.AddDocumentService(); // one line — everything else is internal

var provider = services.BuildServiceProvider();
var documentService = provider.GetRequiredService<IDocumentService>();

// Export
var export = await documentService.ExportAsync(new ExportRequest
{
    Json = salesReportJson,
    Format = ExportFormat.Excel
});
// export.Content is the file bytes — return as a download.

// Import
var import = await documentService.ImportAsync(new ImportRequest
{
    Content = uploadedFileBytes,
    Format = ImportFormat.Csv
});
// import.Json is the common {Columns, Rows} shape.

// Print
var print = await documentService.PrintAsync(new PrintRequest
{
    TemplateName = "SalesInvoice",
    Json = invoiceJson,
    Format = PrintOutputFormat.Pdf
});
// print.Content is the PDF bytes; print.PreviewHtml can be shown in a browser first.
```

The module only ever references `DocumentService.Core` types
(`IDocumentService`, the DTOs, the enums) plus the one
`AddDocumentService()` call at startup.

---

## 7. Extensibility — what changes, what doesn't

| To add...                        | You touch                                             | You do NOT touch |
|-----------------------------------|--------------------------------------------------------|-------------------|
| A new export format (e.g. HTML)   | New `IExportEngine`, one DI line                       | `IExportService`, `ExportEngineFactory`, business code |
| A new import format               | New `IImportEngine`, one DI line                       | `IImportService`, `ImportEngineFactory` |
| A new print template              | New `.html` file in `templates/`                       | `PrintService`, renderers |
| A different Excel/Word/PDF library| Rewrite the one engine class that owns it              | Everything else |
| Company header/logo, barcode, QR, digital signature, template versioning, multi-language templates | `DocumentOptions` / template HTML / a new `ITemplateService` implementation | `IDocumentService`, the Factory pattern |

These future-scope items are intentionally **not implemented** in this
POC — the point was to confirm the seams (`IExportEngine`, `IImportEngine`,
`IPrintRenderer`, `ITemplateService`) are in the right places to support
them later without a redesign.

---

## 8. Known POC limitations (by design)

- `SimpleHtmlParser` understands only `<h1-3>`, `<p>`, and `<table>` — enough
  for the three sample templates, not a general HTML renderer.
- Word import is not implemented (listed as optional in the requirements).
- No persistence anywhere — Import returns JSON and stops; nothing is saved.
- No authentication/authorization — this is a library, not a hosted API.
