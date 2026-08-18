# Document Service — POC Implementation Record

Status snapshot of what is **actually implemented** in this repository today.
This is a POC status document, not an architecture proposal — every claim
below was verified against the current source code, `.csproj` files, and a
real solution build.

---

## 1. Current POC Overview

- **.NET version**: .NET 10 (`net10.0`), C#, `ImplicitUsings`/`Nullable` enabled on all projects. Solution file is `DocumentService.slnx` (new .slnx format).
- **Projects**:
  - `DocumentService.Core` — models, DTOs, enums, interfaces, exception type. No third-party packages beyond `Microsoft.Extensions.Logging.Abstractions`.
  - `DocumentService.Engines` — every engine, factory, service implementation, and the DI registration extension. Owns all third-party libraries.
  - `DocumentService.ConsoleApp` — manual test harness (`Program.cs`), not an API or UI.
- **Implemented**: Export to Excel/CSV/Word/PDF/Markdown, Import from Excel/CSV, Print (HTML template → PDF or Word) via `IDocumentService`.
- **NOT implemented**: Word import, PDF import, Markdown import, any REST/web API layer, persistence of any kind, authentication, a real templating engine (conditionals/loops beyond one level), image support in the Word-print path.
- **Architecture** (as built):

```
ERP module / Console App
        ↓
IDocumentService (DocumentServiceFacade)
        ↓
IExportService | IImportService | IPrintService
        ↓
Factory (ExportEngineFactory | ImportEngineFactory | PrintRendererFactory)
        ↓
Format-specific Engine (one class per format)
        ↓
Generated File (byte[]) / JSON string
```

---

## 2. Libraries / NuGet Packages

Verified directly from the three `.csproj` files.

| Package | Version | Project | Used For | Open Source/Commercial | License if known |
|---|---|---|---|---|---|
| ClosedXML | 0.105.1 | Engines | Excel export & import (`.xlsx`) | Open Source | MIT |
| CsvHelper | 33.1.0 | Engines | CSV export & import | Open Source | Dual: MS-PL / Apache 2.0 |
| DocumentFormat.OpenXml | 3.5.1 | Engines | Word export & Word print rendering (`.docx`) | Open Source | MIT |
| QuestPDF | 2026.7.1 | Engines | PDF **export only** (`PdfExportEngine`) | Source-available | QuestPDF Community license (free under a revenue threshold; commercial license required above it) |
| PuppeteerSharp | 25.3.4 | Engines | PDF **print** rendering (`ChromiumPdfPrintRenderer`) — headless Chromium | Open Source | MIT |
| Microsoft.Extensions.Logging.Abstractions | 10.0.10 | Core, Engines | `ILogger<T>` contracts | Open Source | MIT |
| Microsoft.Extensions.DependencyInjection.Abstractions | 10.0.10 | Engines | DI contracts used inside `AddDocumentService()` | Open Source | MIT |
| Microsoft.Extensions.DependencyInjection | 10.0.10 | ConsoleApp | Concrete DI container (test harness only) | Open Source | MIT |
| Microsoft.Extensions.Logging.Console | 10.0.10 | ConsoleApp | Console log sink (test harness only) | Open Source | MIT |
| System.Text.Json | built into .NET 10 (no NuGet entry) | Core | All JSON parsing/serialization (`DocumentJsonMapper`, `JsonValueConverter`) | Open Source | MIT |

**Not present in this solution** (called out because the request asked specifically): **Markdig is not referenced anywhere** — Markdown export is hand-built with `StringBuilder`, no Markdown library at all. No HTML templating library (Handlebars.Net, Scriban, Fluid, RazorLight) is used — the placeholder engine is a hand-written `Regex`-based class.

---

## 3. Export

Implemented formats, verified against `Export/*.cs` and `ExportEngineFactory`:

| Requested Format | Input | Engine/Class | Package | Output |
|---|---|---|---|---|
| Excel | JSON → `DocumentModel` | `ExcelExportEngine` | ClosedXML | `.xlsx` |
| CSV | JSON → `DocumentModel` | `CsvExportEngine` | CsvHelper | `.csv` |
| Word | JSON → `DocumentModel` | `WordExportEngine` | DocumentFormat.OpenXml | `.docx` |
| PDF | JSON → `DocumentModel` | `PdfExportEngine` | QuestPDF | `.pdf` |
| Markdown | JSON → `DocumentModel` | `MarkdownExportEngine` | none (`StringBuilder`) | `.md` |

Flow (identical shape for all five, only the last two steps differ per format):

```
JSON (ExportRequest.Json)
   ↓
DocumentJsonMapper.FromJson()
   ↓
DocumentModel
   ↓
ExportEngineFactory.GetEngine(ExportFormat)
   ↓
<Format>ExportEngine.GenerateAsync()
   ↓
<library> builds the file in memory
   ↓
byte[] (wrapped in ExportResult: bytes + file name + content type)
```

Notes verified from code:
- Excel: header row is bold (`ExcelExportEngine`, `Style.Font.Bold = true`), columns auto-fit if `DocumentOptions.AutoFitColumns` is true (default), sheet name sanitized to Excel's 31-char/invalid-char rules.
- Word: title paragraph (bold, 16pt) + a single bordered table built cell-by-cell via the Open XML SDK object model — no template file is used for Word *export*.
- PDF export: QuestPDF fluent layout (`Document.Create`), A4, grey header row, page-number footer. Fully independent of the Print pipeline's PDF renderer (see §7).
- Markdown: plain GitHub-style pipe table, `|` characters in cell values are escaped.
- CSV: `CsvHelper`'s `CsvWriter` — correct quoting/escaping of commas/quotes/newlines is handled by the library, not manual string concatenation.

---

## 4. Import

Verified against `Import/*.cs` and `ImportEngineFactory`. Only two formats exist.

| Input File | Engine/Class | Package | Output |
|---|---|---|---|
| `.xlsx` | `ExcelImportEngine` | ClosedXML | JSON |
| `.csv` | `CsvImportEngine` | CsvHelper | JSON |

**NOT supported** (no code exists for these — confirmed by `ImportFormat` enum and `ImportEngineFactory` registrations): Word import, PDF import, Markdown import. `ImportFormat` enum itself only defines `Excel`, `Csv`, `Word` — but no `IImportEngine` implementing `Word` is registered anywhere, so requesting `ImportFormat.Word` throws `DocumentServiceException("No import engine registered for format 'Word'.")` at runtime.

Flow:

```
Uploaded file bytes (ImportRequest.Content)
   ↓
ImportEngineFactory.GetEngine(ImportFormat)
   ↓
<Format>ImportEngine.ReadAsync(Stream)
   ↓
DocumentModel
   ↓
DocumentJsonMapper.ToJson()
   ↓
JSON string (ImportResult.Json)
```

Details verified from code:
- Excel import reads only the **first worksheet**, row 1 = header. `Field` and `Header` are both set to the raw header cell text (no separate business key exists in a raw spreadsheet). Cell values are typed from `IXLCell.DataType` (Number → double, Boolean, DateTime, Blank → null, else string).
- CSV import reads row 1 = header via `CsvHelper`'s `ReadHeader()`. Each value is heuristically parsed: `long` → `double` → `bool` → else raw string.
- **No data is persisted anywhere** — both engines only transform bytes → `DocumentModel` → JSON string and return it.

---

## 5. Print

Verified against `PrintService.cs`, `HtmlTemplateService.cs`, `ChromiumPdfPrintRenderer.cs`, `WordPrintRenderer.cs`.

```
PrintRequest { TemplateName, Json, Format }
        ↓
PrintService.PrintAsync()
        ↓
JsonValueConverter.ParseObject(Json)  →  Dictionary<string, object?>
        ↓
HtmlTemplateService.RenderAsync(TemplateName, data)
   - loads templates/{TemplateName}.html
   - expands {{#each Array}}...{{/each}} blocks
   - replaces {{Field}} placeholders
        ↓
merged HTML string
        ↓
PrintRendererFactory.GetRenderer(PrintOutputFormat)
        ↓
ChromiumPdfPrintRenderer (Pdf)   or   WordPrintRenderer (Word)
        ↓
byte[] (PrintResult.Content) + PrintResult.PreviewHtml (the merged HTML, for optional on-screen preview)
```

- **Templates stored in**: `templates/` at the repo root, copied to `output/templates/` (or `bin/.../templates/`) at build time via `<None Include="..\..\templates\*.html" CopyToOutputDirectory="PreserveNewest">` in `DocumentService.ConsoleApp.csproj`. Resolved at runtime by `PrintServiceOptions.TemplatesDirectory` (default: `AppContext.BaseDirectory/templates`).
- **Naming convention**: `PrintRequest.TemplateName` must exactly match a file `templates/{TemplateName}.html` (case-sensitive on Linux, case-insensitive on Windows). No extension is passed by the caller.
- **`{{Field}}`**: single `Regex.Replace` (`\{\{(\w+)\}\}`) against the top-level data dictionary. Only bare word-character field names match — no dots, no `#`, no `@`.
- **`{{#each Items}}...{{/each}}`**: a separate regex captures the block once, looks up `Items` in the data (must be an `IEnumerable<object?>`), and re-renders the inner block once per item, resolving `{{Field}}` (not `{{this.Field}}`) against each item's own dictionary. Only **one level** of nesting is supported — no nested `{{#each}}` inside a `{{#each}}`.
- **JSON mapping**: `JsonValueConverter.ParseObject` turns arbitrary JSON into `Dictionary<string, object?>`, with nested JSON objects becoming nested dictionaries and JSON arrays becoming `List<object?>` — this is the same converter `DocumentJsonMapper` uses internally for Export/Import.
- **PDF rendering**: `ChromiumPdfPrintRenderer` — see §7.
- **Word rendering**: `WordPrintRenderer` — see §8.

---

## 6. Template Handling

**Text placeholder — confirmed working:**

Template:
```
{{CustomerName}}
```
JSON:
```json
{ "CustomerName": "ABC Traders" }
```
Result: `ABC Traders`

**Repeating collection — confirmed working (one level only):**

```
{{#each Items}}
{{Description}} - {{Qty}}
{{/each}}
```
With `"Items": [{"Description":"Widget","Qty":2}]` → repeats the line once per array entry.

**Support matrix — verified by reading `HtmlTemplateService` and `SimpleHtmlParser` directly:**

| Feature | Supported? | Notes |
|---|---|---|
| Plain text placeholders `{{Field}}` | ✅ Yes | Regex `\{\{(\w+)\}\}` only — word characters, no dots |
| Tables (`<table>`) | ✅ Yes, for both PDF and Word | Chromium renders `<table>` natively for PDF; `SimpleHtmlParser` extracts `<table>/<tr>/<th>/<td>` for Word |
| Loops `{{#each Array}}...{{/each}}` | ✅ Yes, one level | No nested `{{#each}}`, no `{{this.Field}}`, no `{{@index}}` |
| Nested field access `{{this.Field}}`, `{{../Field}}` | ❌ **POC / Limitation** | Regex doesn't match dots — left as literal unresolved text in the output |
| Conditionals `{{#if}}` / `{{#unless}}` | ❌ **POC / Limitation** | Not implemented at all — left as literal unresolved text |
| `{{@index}}` / `{{@first}}` | ❌ **POC / Limitation** | Not implemented |
| Triple-brace `{{{Field}}}` (unescaped HTML) | ❌ **POC / Limitation** | Doesn't match the double-brace regex; left as literal text |
| Images (`<img>`) | ⚠️ **Split by output format** | PDF: ✅ full support (real Chromium — `data:` URIs and `https://` URLs both render). Word: ❌ **POC / Limitation** — `SimpleHtmlParser` strips all `<img>` tags |
| CSS (colors, flexbox, grid, `@media print`) | ⚠️ **Split by output format** | PDF: ✅ full support (real browser engine). Word: ❌ **POC / Limitation** — all CSS is stripped, only heading/paragraph/table text survives |
| Complex/arbitrary HTML (`<div>`, custom layout) | ⚠️ **Split by output format** | PDF: ✅ fully supported. Word: ❌ only `<h1-3>`, `<p>`, `<table>` survive; everything else is stripped |
| Nested JSON objects as data | ✅ Parsed correctly by `JsonValueConverter` | But template syntax to walk into them (`{{../Field}}`, dotted paths) is not supported — see above |

---

## 7. PDF Rendering

**Two entirely separate PDF code paths exist in this solution — do not confuse them:**

### Export PDF (`ExportFormat.Pdf`)
```
DocumentModel
   ↓
PdfExportEngine (QuestPDF)
   ↓
PDF byte[]
```
Uses **QuestPDF 2026.7.1** exclusively. Lays out `DocumentModel` (title/columns/rows) using QuestPDF's own fluent C# layout DSL — it does not consume HTML at all. This is a **POC-only PDF path** in the sense that QuestPDF's Community license has a revenue threshold; commercial use above that threshold requires a paid license.

### Print PDF (`PrintOutputFormat.Pdf`)
```
Merged HTML (from HtmlTemplateService)
   ↓
ChromiumPdfPrintRenderer (PuppeteerSharp → headless Chromium)
   ↓
PDF byte[]
```
Uses **PuppeteerSharp 25.3.4** — launches a real, headless Chromium browser (auto-downloaded on first use), sets the merged HTML as the page content, waits for images to finish loading, forces print-media CSS (`EmulateMediaTypeAsync(MediaType.Print)`), and calls Chromium's native `page.pdf()` (A4, background printing on, `PreferCSSPageSize` on). This is a real browser engine — full CSS/image fidelity is genuinely implemented and was verified by rendering actual templates.

**Possible Future Integration / Not Currently Implemented**: the original requirements mention an existing org "Render-PDF" renderer and a "Certificate Designer" templating engine. **Neither exists in this codebase.** `ChromiumPdfPrintRenderer` was built as a stand-in that satisfies the same `IPrintRenderer` contract, so swapping in a real Render-PDF service later means implementing that one interface — no other code changes. Do not describe Render-PDF as currently integrated; it is not referenced anywhere in this solution.

---

## 8. Word Generation

Two Word code paths, both using the same library, verified in `Export/WordExportEngine.cs` and `Print/WordPrintRenderer.cs`:

```
JSON → DocumentModel → WordExportEngine → DocumentFormat.OpenXml → .docx   (Export pipeline)
Merged HTML → SimpleHtmlParser (blocks) → WordPrintRenderer → DocumentFormat.OpenXml → .docx   (Print pipeline)
```

`DocumentFormat.OpenXml` (Open XML SDK, 3.5.1) is used directly in both `WordExportEngine.cs` and `WordPrintRenderer.cs` — it is the only Word-capable package in the solution. Both engines build the `.docx` **directly** via the OOXML object model (`WordprocessingDocument.Create`, manual `Paragraph`/`Run`/`Table` construction) — there is no conversion from an intermediate format (no HTML-to-Word conversion library is used). For the Print pipeline, `WordPrintRenderer` only sees the small block model (`HeadingBlock`/`ParagraphBlock`/`TableBlock`) produced by `SimpleHtmlParser`, so anything beyond `<h1-3>/<p>/<table>` in a template is invisible to the Word output (see §6 limitations).

---

## 9. Excel Generation

```
JSON → DocumentModel → ExcelExportEngine → ClosedXML → .xlsx
```

Verified from `ExcelExportEngine.cs`:
- **Headers**: row 1, one cell per `DocumentColumn.Header`, `Font.Bold = true` — only if `DocumentOptions.IncludeHeaderRow` is true (default).
- **Rows**: one `DocumentModel.Rows` entry per Excel row; each cell value is looked up by `DocumentColumn.Field` and typed (`long`/`double`/`bool`/`DateTime`/string) via `SetCellValue`.
- **Columns**: one Excel column per `DocumentColumn` entry, in the order given.
- **Styling**: only bold headers are implemented — no colors, borders, cell fills, or number formats.
- **Auto-fit**: `worksheet.Columns().AdjustToContents()` — only if `DocumentOptions.AutoFitColumns` is true (default).
- **Templates for Excel export**: **none** — Excel export is programmatic only; there is no `.xlsx` template file mechanism anywhere in the code (unlike Print, which uses HTML template files).

---

## 10. CSV

```
JSON → DocumentModel → CsvExportEngine → CsvHelper → .csv
```

Verified from `CsvExportEngine.cs`:
- **Headers**: one `CsvWriter.WriteField()` call per `DocumentColumn.Header`, written as row 1 — only if `IncludeHeaderRow` is true.
- **Rows**: one CSV row per `DocumentModel.Rows` entry; each field value looked up by `DocumentColumn.Field` and written via `CsvWriter.WriteField(value)` (CsvHelper handles quoting/escaping automatically for commas, quotes, and newlines in the value).
- No manual string concatenation is used anywhere in this engine.

---

## 11. Markdown

Verified from `MarkdownExportEngine.cs` — **no Markdown library is used at all**, contrary to what might be assumed:

- Built entirely with `System.Text.StringBuilder`.
- **Markdig is NOT referenced** in any `.csproj` — confirmed by inspecting `DocumentService.Engines.csproj`'s package list directly.
- Output is a `# Title` heading followed by a GitHub-style pipe table (`| --- | --- |` separator row), with `|` characters in cell values escaped as `\|`.

**What is currently implemented**: Markdown **export only** (`DocumentModel` → `.md` bytes).

**What is NOT implemented** (no code exists for any of these):
- Markdown import
- Markdown → HTML conversion
- Markdown → PDF conversion
- Any Markdown parsing at all — the engine only ever *writes* Markdown text, never reads it

---

## 12. Common Service API

The single public entry point, verified in `IDocumentService.cs` and `DocumentServiceFacade.cs`:

```csharp
public interface IDocumentService
{
    Task<ExportResult> ExportAsync(ExportRequest request, CancellationToken cancellationToken = default);
    Task<ImportResult> ImportAsync(ImportRequest request, CancellationToken cancellationToken = default);
    Task<PrintResult> PrintAsync(PrintRequest request, CancellationToken cancellationToken = default);
}
```

Request/response DTOs (all in `DocumentService.Core.DTOs`):

| DTO | Fields |
|---|---|
| `ExportRequest` | `string Json`, `ExportFormat Format` |
| `ExportResult` | `byte[] Content`, `string FileName`, `string ContentType` |
| `ImportRequest` | `byte[] Content`, `ImportFormat Format`, `string? FileName` |
| `ImportResult` | `string Json` |
| `PrintRequest` | `string TemplateName`, `string Json`, `PrintOutputFormat Format` |
| `PrintResult` | `byte[] Content`, `string FileName`, `string ContentType`, `string? PreviewHtml` |

**How the ERP (or any caller) uses it** — actual usage shown in `Program.cs`:

```csharp
var services = new ServiceCollection();
services.AddLogging(b => b.AddConsole());
services.AddDocumentService();               // one-line registration of everything

var provider = services.BuildServiceProvider();
var documentService = provider.GetRequiredService<IDocumentService>();

var export = await documentService.ExportAsync(new ExportRequest
{
    Json = salesReportJson,
    Format = ExportFormat.Excel
});
// export.Content -> file bytes
```

The caller only ever references `DocumentService.Core` types — no engine, factory, or third-party type (ClosedXML/OpenXml/CsvHelper/QuestPDF/PuppeteerSharp) is visible outside `DocumentService.Engines`.

---

## 13. Factory / Strategy Architecture

Three factories exist, all following the same pattern, verified in `Factory/*.cs` and `Print/PrintRendererFactory.cs`:

```csharp
public class ExportEngineFactory : IExportEngineFactory
{
    private readonly IReadOnlyDictionary<ExportFormat, IExportEngine> _engines;
    public ExportEngineFactory(IEnumerable<IExportEngine> engines) =>
        _engines = engines.ToDictionary(e => e.Format);
    public IExportEngine GetEngine(ExportFormat format) => _engines[format]; // (simplified — throws DocumentServiceException if missing)
}
```

`ImportEngineFactory` and `PrintRendererFactory` are structurally identical, keyed on `ImportFormat` and `PrintOutputFormat` respectively. Every engine self-registers its `Format` property; DI supplies the whole set via `IEnumerable<IExportEngine>` (etc.) — the factory never has a hardcoded `switch`.

```
ExportFormat.Excel  → ExcelExportEngine  → ClosedXML
ExportFormat.Csv    → CsvExportEngine    → CsvHelper
ExportFormat.Word   → WordExportEngine   → DocumentFormat.OpenXml
ExportFormat.Pdf    → PdfExportEngine    → QuestPDF
ExportFormat.Markdown → MarkdownExportEngine → (StringBuilder)

ImportFormat.Excel  → ExcelImportEngine  → ClosedXML
ImportFormat.Csv    → CsvImportEngine    → CsvHelper

PrintOutputFormat.Pdf  → ChromiumPdfPrintRenderer → PuppeteerSharp
PrintOutputFormat.Word → WordPrintRenderer         → DocumentFormat.OpenXml
```

---

## 14. End-to-End Examples

### A. Export Sales Invoice as Excel
```
Sales Invoice screen
   ↓
Get data, build DocumentModel-shaped JSON
   ↓
IDocumentService.ExportAsync(Json, ExportFormat.Excel)
   ↓
ExportEngineFactory → ExcelExportEngine
   ↓
ClosedXML (XLWorkbook)
   ↓
.xlsx byte[]
   ↓
Caller writes bytes to a file / returns as download
```

### B. Export as CSV
```
Same JSON
   ↓
ExportEngineFactory → CsvExportEngine
   ↓
CsvHelper (CsvWriter)
   ↓
.csv byte[]
```

### C. Export as Word
```
Same JSON
   ↓
ExportEngineFactory → WordExportEngine
   ↓
DocumentFormat.OpenXml (WordprocessingDocument)
   ↓
.docx byte[]
```

### D. Export as PDF
```
Same JSON
   ↓
ExportEngineFactory → PdfExportEngine
   ↓
QuestPDF (Document.Create)
   ↓
.pdf byte[]
```

### E. Export as Markdown
```
Same JSON
   ↓
ExportEngineFactory → MarkdownExportEngine
   ↓
StringBuilder (manual table build)
   ↓
.md byte[]
```

### F. Import Excel
```
Upload .xlsx (byte[])
   ↓
IDocumentService.ImportAsync(bytes, ImportFormat.Excel)
   ↓
ImportEngineFactory → ExcelImportEngine
   ↓
ClosedXML reads first worksheet, row 1 = header
   ↓
DocumentModel
   ↓
DocumentJsonMapper.ToJson()
   ↓
JSON string returned to caller (nothing persisted)
```

### G. Print Sales Invoice
```
Sales Invoice data
   ↓
JSON (matches template's {{Field}} names)
   ↓
IDocumentService.PrintAsync(TemplateName: "SalesInvoice", Json, Format: Pdf)
   ↓
HtmlTemplateService merges JSON into templates/SalesInvoice.html
   ↓
Merged HTML
   ↓
PrintRendererFactory → ChromiumPdfPrintRenderer
   ↓
Headless Chromium (PuppeteerSharp)
   ↓
.pdf byte[]
```

---

## 15. Current Limitations

**Supported formats**: Export — Excel, CSV, Word, PDF, Markdown. Import — Excel, CSV only. Print — PDF, Word.

**NOT supported**: Word import, PDF import, Markdown import/parsing, HTML export, JSON export (as a distinct "format" — the input already is JSON), XML export, any web/REST API layer.

**Template limitations**: one level of `{{#each}}` only; no `{{this.Field}}`, no `{{#if}}`/`{{#unless}}`, no `{{@index}}`/`{{@first}}`, no `{{../Field}}` parent-scope access, no triple-brace unescaped output. Any of these left as literal unresolved text in the output.

**HTML limitations**: full HTML/CSS support only on the **PDF print path** (real Chromium). The **Word print path** only understands `<h1-3>`, `<p>`, `<table>/<tr>/<th>/<td>` — everything else (images, divs, CSS, styling) is silently stripped.

**PDF limitations**: two independent PDF engines exist (QuestPDF for Export, Chromium/PuppeteerSharp for Print) with different capabilities and no shared code — a template-driven PDF only works through the Print pipeline, not Export. QuestPDF's license has a revenue-based free tier, not free at unlimited commercial scale.

**Word limitations**: no image support, no CSS/styling support, only three HTML element types recognized.

**Import limitations**: no persistence (by design), no validation that uploaded bytes actually match the declared format, first-worksheet-only for Excel.

**Image limitations**: PDF print supports `data:` URIs and `https://` URLs (verified). Word print supports no images at all. Excel/Word/PDF/Markdown *export* have no image support in any form (`DocumentModel` has no image field).

**Styling limitations**: Excel export — bold headers + auto-fit only, no colors/borders/number formats. Word export — bold title + bordered table only. Markdown — plain text tables only.

**Security limitations**: no authentication/authorization anywhere (this is a library, not a hosted service); no validation that `ImportRequest.Content` matches the declared `ImportFormat`; `ChromiumPdfPrintRenderer` will make real outbound HTTPS requests for any `<img src="https://...">` in a template, which is a template-authoring concern (SSRF-adjacent) worth reviewing before accepting user-supplied templates in production.

**Performance considerations**: `ChromiumPdfPrintRenderer` launches Chromium lazily and reuses one browser instance for the process lifetime (via a `SemaphoreSlim`-guarded singleton), but each `RenderAsync` call opens a new page — first render also pays a one-time Chromium download cost (auto-downloaded via `BrowserFetcher`, cached after). Excel import loads the whole workbook into memory via `RangeUsed()`; no streaming for very large files anywhere in the solution.

**POC-only code**: `SimpleHtmlParser` (regex-based, not a real HTML parser); `HtmlTemplateService`'s placeholder engine (regex-based, not a real template engine); `MarkdownExportEngine` (no Markdown library); `DocumentService.ConsoleApp` (manual test harness, not a real host).

---

## 16. Production Readiness

**POC Status: Not production ready. Ready for further development.**

Reasons:
- No hosting layer exists (no ASP.NET Core API, no auth) — this is a class library plus a console test harness only.
- No automated tests exist anywhere in the solution — correctness has only been verified by manually running `Program.cs` and visually inspecting generated files.
- Template engine (`HtmlTemplateService`) is a minimal regex-based placeholder replacer, not a real templating library — will need to be extended or replaced (e.g. Handlebars.Net) before templates requiring conditionals/nested loops can be used.
- Two separate, non-interoperable PDF engines exist (QuestPDF for Export, Chromium for Print) — worth deciding whether that duplication is acceptable long-term or whether Export should also move to the Chromium path for consistency.
- QuestPDF's licensing needs a decision before commercial ERP deployment (Community tier has a revenue ceiling).
- No validation, retry, or resiliency logic around the Chromium process (crash recovery, memory limits, concurrent-render throttling) — fine for a POC's single-threaded console demo, not verified under load.
- Missing components for production: hosting/API layer, auth, input size limits, structured configuration (all config today is code-only, e.g. `TemplatesDirectory` default), integration tests, and a decision on the Render-PDF/Certificate Designer integration mentioned in the original requirements (neither exists yet).

What **is** solid and reusable as-is: the Strategy/Factory architecture, the `IDocumentService` facade boundary, and the individual format engines (ClosedXML/CsvHelper/OpenXml usage) — these follow clean, conventional patterns and would need little rework to sit behind a real API.

---

## 17. Quick Reference

| Requirement | Input | Package | Engine | Output |
|---|---|---|---|---|
| Excel Export | JSON | ClosedXML | `ExcelExportEngine` | `.xlsx` |
| CSV Export | JSON | CsvHelper | `CsvExportEngine` | `.csv` |
| Word Export | JSON | DocumentFormat.OpenXml | `WordExportEngine` | `.docx` |
| PDF Export | JSON | QuestPDF | `PdfExportEngine` | `.pdf` |
| Markdown Export | JSON | none (`StringBuilder`) | `MarkdownExportEngine` | `.md` |
| Excel Import | `.xlsx` | ClosedXML | `ExcelImportEngine` | JSON |
| CSV Import | `.csv` | CsvHelper | `CsvImportEngine` | JSON |
| Print PDF | JSON + HTML template | PuppeteerSharp (headless Chromium) | `ChromiumPdfPrintRenderer` | `.pdf` |
| Print Word | JSON + HTML template | DocumentFormat.OpenXml | `WordPrintRenderer` | `.docx` |
| Word Import | — | — | **not implemented** | — |
| PDF Import | — | — | **not implemented** | — |
| Markdown Import | — | — | **not implemented** | — |
