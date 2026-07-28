using System.Diagnostics;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls("http://0.0.0.0:9999");

var app = builder.Build();

var wwwrootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot");
var fileProvider = new PhysicalFileProvider(wwwrootPath);

// Serve Blazor WASM framework files
var frameworkPath = Path.Combine(wwwrootPath, "_framework");
if (Directory.Exists(frameworkPath))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(frameworkPath),
        RequestPath = "/_framework",
        ServeUnknownFileTypes = true
    });
}

// Serve module DLLs
var modulesRoot = Path.Combine(wwwrootPath, "modules");
if (Directory.Exists(modulesRoot))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(modulesRoot),
        RequestPath = "/modules",
        ServeUnknownFileTypes = true,
        DefaultContentType = "application/octet-stream"
    });
}

// Serve default files (index.html) then all static files from wwwroot
app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = fileProvider });
app.UseStaticFiles(new StaticFileOptions { FileProvider = fileProvider });

// SPA fallback
app.MapFallbackToFile("index.html", new StaticFileOptions { FileProvider = fileProvider });

// Auto-open browser (localhost for browser, 0.0.0.0 for LAN access)
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
lifetime.ApplicationStarted.Register(() =>
{
    try { Process.Start(new ProcessStartInfo("http://localhost:9999") { UseShellExecute = true }); }
    catch { }
});

app.Run();
