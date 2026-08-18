using DocumentService.Core.Enums;
using DocumentService.Core.Exceptions;
using DocumentService.Core.Interfaces;
using Microsoft.Extensions.Logging;
using PuppeteerSharp;
using PuppeteerSharp.Media;

namespace DocumentService.Engines.Print;

/// <summary>
/// Renders a fully-merged HTML string into a PDF using a real, headless Chromium
/// instance (via PuppeteerSharp), instead of re-implementing HTML/CSS layout.
/// The browser engine handles 100% of the rendering — this class only feeds it
/// the HTML and reads the PDF bytes back out, so grid/flexbox layouts, colors,
/// @media print rules, and images (data: URIs and https:// URLs alike) render
/// exactly as authored, with no intermediate parsing or layout code of our own.
///
/// Chromium is launched once (lazily, on first use) and reused for the lifetime
/// of the process; it is downloaded automatically on first run via BrowserFetcher
/// if not already present in the local PuppeteerSharp cache.
/// </summary>
public class ChromiumPdfPrintRenderer : IPrintRenderer, IAsyncDisposable
{
    private readonly ILogger<ChromiumPdfPrintRenderer> _logger;
    private readonly SemaphoreSlim _launchLock = new(1, 1);
    private IBrowser? _browser;

    public ChromiumPdfPrintRenderer(ILogger<ChromiumPdfPrintRenderer> logger)
    {
        _logger = logger;
    }

    public PrintOutputFormat Format => PrintOutputFormat.Pdf;

    public async Task<byte[]> RenderAsync(string html, CancellationToken cancellationToken = default)
    {
        try
        {
            var browser = await GetBrowserAsync(cancellationToken);

            await using var page = await browser.NewPageAsync();

            // The merged HTML is a complete document already (from HtmlTemplateService),
            // so we hand it straight to Chromium — no navigation, no external base URL needed
            // for https:// image sources or data: URI images, both of which Chromium resolves natively.
            await page.SetContentAsync(html);

            // SetContentAsync does not reliably wait for network activity (PuppeteerSharp's own
            // wait-until options are documented as not working with SetContent), so external
            // https:// <img> sources may still be loading. Wait for every image to either finish
            // or fail, with a per-image safety timeout so an unreachable URL can't hang rendering.
            await page.EvaluateFunctionAsync(@"() => Promise.all(Array.from(document.images).map(img => {
                if (img.complete) return Promise.resolve();
                return new Promise(resolve => {
                    img.addEventListener('load', resolve, { once: true });
                    img.addEventListener('error', resolve, { once: true });
                    setTimeout(resolve, 10000);
                });
            }))");

            // Explicitly force print-media CSS (not screen CSS), so @media print rules
            // in the templates are guaranteed to apply regardless of library defaults.
            await page.EmulateMediaTypeAsync(MediaType.Print);

            var pdfBytes = await page.PdfDataAsync(new PdfOptions
            {
                Format = PaperFormat.A4,
                PrintBackground = true,
                PreferCSSPageSize = true,
                MarginOptions = new MarginOptions { Top = "0", Bottom = "0", Left = "0", Right = "0" }
            });

            return pdfBytes;
        }
        catch (Exception ex) when (ex is not DocumentServiceException)
        {
            _logger.LogError(ex, "PDF print rendering (Chromium) failed");
            throw new DocumentServiceException("Failed to render PDF document.", ex);
        }
    }

    private async Task<IBrowser> GetBrowserAsync(CancellationToken cancellationToken)
    {
        if (_browser is { IsClosed: false })
        {
            return _browser;
        }

        await _launchLock.WaitAsync(cancellationToken);
        try
        {
            if (_browser is { IsClosed: false })
            {
                return _browser;
            }

            _logger.LogInformation("Launching headless Chromium for PDF print rendering (downloading it first if not already cached)");

            await new BrowserFetcher().DownloadAsync();
            _browser = await Puppeteer.LaunchAsync(new LaunchOptions { Headless = true });

            return _browser;
        }
        finally
        {
            _launchLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser is not null)
        {
            await _browser.CloseAsync();
        }

        _launchLock.Dispose();
    }
}
