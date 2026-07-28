namespace DocumentService.Engines.Print;

/// <summary>
/// Configuration for the Print pipeline. Registered as a singleton so the
/// templates folder location can be set once at startup (e.g. from appsettings).
/// </summary>
public class PrintServiceOptions
{
    public string TemplatesDirectory { get; set; } = Path.Combine(AppContext.BaseDirectory, "templates");
}
