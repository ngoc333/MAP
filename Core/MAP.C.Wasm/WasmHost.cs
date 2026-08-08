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
        WebAssemblyHost? host = null;
        ILogger? startupLogger = null;

        try
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);

            // Register MainLayout as root component (no Router needed)
            builder.RootComponents.Add<MainLayout>("#app");
            builder.RootComponents.Add<HeadOutlet>("head::after");

            var http = new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) };

            // Register Wasm platform services
            builder.Services.AddWasm(http);

            // Register IDbApiClient as lazy — actual initialization after host build
            // so _jsRuntime is available for error display if db-api.json is missing/malformed.
            DbApiConfiguration? dbApiConfig = null;
            builder.Services.AddSingleton<IDbApiClient>(_ =>
            {
                if (dbApiConfig is null)
                    throw new InvalidOperationException("DB API configuration not loaded.");
                return new DbApiClient(
                    new HttpClient
                    {
                        BaseAddress = dbApiConfig.OracleBaseAddress,
                        Timeout = TimeSpan.FromSeconds(10)
                    },
                    new HttpClient
                    {
                        BaseAddress = dbApiConfig.PostgreSqlBaseAddress,
                        Timeout = TimeSpan.FromSeconds(10)
                    });
            });

            var host0 = builder.Build();
            host = host0;

            // Capture JS runtime for startup error display
            _jsRuntime = host.Services.GetService<IJSRuntime>();

            // Initialize language service
            var langService = host.Services.GetRequiredService<ILanguageService>();
            if (langService is JsonLanguageService jsonLang)
                await jsonLang.InitializeAsync(typeof(JsonLanguageService).Assembly);

            // Create logger (available for all subsequent operations)
            startupLogger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("AppStartup");
            startupLogger.LogInformation(
                "Web application starting. SessionId={SessionId} BaseAddress={BaseAddress} DurationMs={DurationMs}",
                DiagnosticContext.SessionId, builder.HostEnvironment.BaseAddress,
                Stopwatch.GetElapsedTime(started).TotalMilliseconds);

            // Load DB API configuration — after _jsRuntime is captured so errors are visible.
            // Throws on missing or malformed db-api.json (startup fatal).
            await using var dbApiStream = await http.GetStreamAsync("db-api.json");
            dbApiConfig = await DbApiConfiguration.LoadAsync(dbApiStream);

            // Load app configuration
            await host.Services.GetRequiredService<AppConfigService>().LoadAsync();

            // Validate all required startup resources
            await ValidateStartupAsync(host.Services, startupLogger);
        }
        catch (Exception ex)
        {
            // Log full exception when logger is available
            startupLogger?.LogError(ex, "Startup failed");

            // Console fallback for errors before logging services exist
            if (startupLogger is null)
                Console.Error.WriteLine(ex);

            var error = BuildStartupErrorMessage(ex);
            Console.Error.WriteLine($"[MAP Startup Error] {error}");

            ShowStartupError(error);
            return;
        }

        // Normal application lifetime — outside startup error boundary.
        // Runtime exceptions here are NOT startup errors.
        await host.RunAsync();
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

        // 2. Determine startup page:
        //    - First run (no config): validate system-config
        //    - Configured default page: validate DefaultPageId
        //    - Otherwise: no startup page validation required
        var configService = services.GetRequiredService<IAppConfigService>();

        string? startupPageId = null;

        if (!configService.Exists)
        {
            startupPageId = "system-config";
            logger.LogInformation("Startup validation: first-run detected, validating system-config.");
        }
        else if (configService.Current?.DefaultPageId is { Length: > 0 } defaultPageId)
        {
            startupPageId = defaultPageId;
        }

        if (startupPageId is null)
        {
            logger.LogInformation("Startup validation passed (no startup page to validate).");
            return;
        }

        // 3. Validate the startup page exists and is a page
        var menuItem = menuService.FindById(startupPageId)
            ?? throw new InvalidOperationException(
                $"Startup page '{startupPageId}' was not found in menu configuration.");

        if (!menuItem.IsPage)
        {
            throw new InvalidOperationException(
                $"Startup page '{startupPageId}' is not a page.");
        }

        // 4. Validate the module can be loaded
        var moduleLoader = services.GetRequiredService<IModuleLoader>();
        await moduleLoader.LoadComponentAsync(menuItem);
        logger.LogInformation("Startup validation: page loaded. PageId={PageId} Assembly={Assembly} Component={Component}",
            startupPageId, menuItem.Assembly, menuItem.Component);

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
