using DocumentService.Core.Interfaces;
using DocumentService.Engines.Export;
using DocumentService.Engines.Factory;
using DocumentService.Engines.Import;
using DocumentService.Engines.Print;
using DocumentService.Engines.Services;
using Microsoft.Extensions.DependencyInjection;
using QuestPDF.Infrastructure;

namespace DocumentService.Engines.DependencyInjection;

/// <summary>
/// Single composition-root entry point. An ERP module (or this POC's console app)
/// calls services.AddDocumentService() once and only ever resolves IDocumentService —
/// every engine, factory and renderer registered here is an implementation detail.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDocumentService(this IServiceCollection services, Action<PrintServiceOptions>? configurePrint = null)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var printOptions = new PrintServiceOptions();
        configurePrint?.Invoke(printOptions);
        services.AddSingleton(printOptions);

        // Export engines (Strategy pattern — one implementation per ExportFormat)
        services.AddSingleton<IExportEngine, ExcelExportEngine>();
        services.AddSingleton<IExportEngine, CsvExportEngine>();
        services.AddSingleton<IExportEngine, WordExportEngine>();
        services.AddSingleton<IExportEngine, PdfExportEngine>();
        services.AddSingleton<IExportEngine, MarkdownExportEngine>();
        services.AddSingleton<IExportEngineFactory, ExportEngineFactory>();

        // Import engines
        services.AddSingleton<IImportEngine, ExcelImportEngine>();
        services.AddSingleton<IImportEngine, CsvImportEngine>();
        services.AddSingleton<IImportEngineFactory, ImportEngineFactory>();

        // Print pipeline
        services.AddSingleton<ITemplateService, HtmlTemplateService>();
        services.AddSingleton<IPrintRenderer, PdfPrintRenderer>();
        services.AddSingleton<IPrintRenderer, WordPrintRenderer>();
        services.AddSingleton<IPrintRendererFactory, PrintRendererFactory>();
        services.AddSingleton<IPrintService, PrintService>();

        // Business-facing services
        services.AddSingleton<IExportService, ExportService>();
        services.AddSingleton<IImportService, ImportService>();
        services.AddSingleton<IDocumentService, DocumentServiceFacade>();

        return services;
    }
}
