using System.Diagnostics;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using MAP.C.Contract.Config;
using MAP.C.Contract.Database;
using MAP.C.Contract.Localization;
using MAP.C.Contract.Logging;
using MAP.C.Contract.Menus;
using MAP.C.Contract.Modules;
using MAP.C.Runtime.Database;
using MAP.C.Runtime.Localization;
using MAP.C.Wasm.Config;
using MAP.C.UI.Layout;

namespace MAP.C.Wasm;

public static class WasmHost
{
    private static IJSRuntime? _jsRuntime;

    public static async Task RunAsync(string[] args)
    {
        var started = Stopwatch.GetTimestamp();

        try
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);

            // Register MainLayout as root component (no Router needed)
            builder.RootComponents.Add<MainLayout>("#app");
            builder.RootComponents.Add<HeadOutlet>("head::after");

            var http = new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) };

            // Register Wasm platform services
            builder.Services.AddWasm(http);

            // Register database API configuration (throws on invalid config)
            await RegisterDbApiAsync(builder, http);

            var host = builder.Build();

            // Capture JS runtime for startup error display
            _jsRuntime = host.Services.GetService<IJSRuntime>();

            // Initialize language service
            var langService = host.Services.GetRequiredService<ILanguageService>();
            if (langService is JsonLanguageService jsonLang)
                await jsonLang.InitializeAsync(typeof(JsonLanguageService).Assembly);

            // Log startup
            var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("AppStartup");
            logger.LogInformation(
                "Web application starting. SessionId={SessionId} BaseAddress={BaseAddress} DurationMs={DurationMs}",
                DiagnosticContext.SessionId, builder.HostEnvironment.BaseAddress,
                Stopwatch.GetElapsedTime(started).TotalMilliseconds);

            // Load configuration
            await host.Services.GetRequiredService<AppConfigService>().LoadAsync();

            // Validate all required startup resources
            await ValidateStartupAsync(host.Services, logger);

            // Startup boundary ends here — runtime failures are not startup errors
            await host.RunAsync();
        }
        catch (Exception ex)
        {
            var error = BuildStartupErrorMessage(ex);
            System.Console.Error.WriteLine($"[MAP Startup Error] {error}");

            ShowStartupError(error);
        }
    }

    /// <summary>
    /// Shows startup error before the Blazor host exists.
    /// Uses the mapStartupError JS helper injected in index.html when IJSRuntime is available,
    /// otherwise falls back to DOM injection via eval.
    /// </summary>
    private static void ShowStartupError(string message)
    {
        // Try mapStartupError helper (from index.html)
        try
        {
            if (_jsRuntime is IJSInProcessRuntime jsInProcess)
            {
                jsInProcess.InvokeVoid("mapStartupError", message);
                return;
            }
        }
        catch
        {
            // mapStartupError not available
        }

        // Fallback: inject error directly into the #app element
        try
        {
            if (_jsRuntime is IJSInProcessRuntime jsFallback)
            {
                jsFallback.InvokeVoid("eval",
                    $"document.getElementById('app').innerHTML='<pre style=\"padding:16px;color:red;font-family:monospace;white-space:pre-wrap\">{EscapeJs(message)}</pre>'");
                return;
            }
        }
        catch
        {
            // All JS mechanisms unavailable
        }
    }

    private static string EscapeJs(string s) =>
        s.Replace("\\", "\\\\")
         .Replace("'", "\\'")
         .Replace("\n", "\\n")
         .Replace("\r", "\\r")
         .Replace("<", "\\x3c")
         .Replace(">", "\\x3e");

    private static async Task RegisterDbApiAsync(WebAssemblyHostBuilder builder, HttpClient http)
    {
        await using var dbApiConfigurationStream = await http.GetStreamAsync("db-api.json");
        var dbApiConfiguration = await DbApiConfiguration.LoadAsync(dbApiConfigurationStream);
        builder.Services.AddWasmDbApi(dbApiConfiguration);
    }

    /// <summary>
    /// Validates all required startup resources before entering normal operation.
    /// Throws if any required resource is missing or invalid.
    /// </summary>
    private static async Task ValidateStartupAsync(IServiceProvider services, ILogger logger)
    {
        // 1. Validate menu configuration loads successfully
        var menuService = services.GetRequiredService<IMenuService>();
        await menuService.LoadMenusAsync();
        logger.LogInformation("Startup validation: menu loaded. MenuCount={MenuCount}", menuService.Menus.Count);

        // 2. Validate default page if configured
        var configService = services.GetRequiredService<IAppConfigService>();
        var config = configService.Current;
        if (config?.DefaultPageId is { Length: > 0 } defaultPageId)
        {
            var menuItem = menuService.FindById(defaultPageId)
                ?? throw new InvalidOperationException(
                    $"Configured default page '{defaultPageId}' was not found in menu configuration.");

            if (!menuItem.IsPage)
            {
                throw new InvalidOperationException(
                    $"Configured default page '{defaultPageId}' is not a page.");
            }

            var moduleLoader = services.GetRequiredService<IModuleLoader>();
            await moduleLoader.LoadComponentAsync(menuItem);
            logger.LogInformation("Startup validation: default page loaded. PageId={PageId} Assembly={Assembly} Component={Component}",
                defaultPageId, menuItem.Assembly, menuItem.Component);
        }

        logger.LogInformation("Startup validation passed.");
    }

    private static string BuildStartupErrorMessage(Exception exception)
    {
        var inner = exception;
        while (inner.InnerException is not null)
            inner = inner.InnerException;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("MAP Startup Error");
        sb.AppendLine();

        if (exception.Message != inner.Message)
        {
            sb.AppendLine(exception.Message);
            sb.AppendLine();
        }

        sb.AppendLine("Error:");
        sb.AppendLine(inner.Message);
        sb.AppendLine();
        sb.AppendLine("Session:");
        sb.AppendLine(DiagnosticContext.SessionId);
        sb.AppendLine();
        sb.AppendLine("The application cannot continue.");

        return sb.ToString();
    }
}
