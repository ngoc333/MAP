using System.Diagnostics;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MAP.C.Contract.Config;
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
    public static async Task RunAsync(string[] args)
    {
        var started = Stopwatch.GetTimestamp();
        WebAssemblyHost? host = null;
        ILogger? logger = null;

        try
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);

            builder.RootComponents.Add<MainLayout>("#app");
            builder.RootComponents.Add<HeadOutlet>("head::after");

            var http = new HttpClient
            {
                BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
            };

            builder.Services.AddWasm(http);

            await using var stream =
                await http.GetStreamAsync("db-api.json");

            var dbConfig =
                await DbApiConfiguration.LoadAsync(stream);

            builder.Services.AddWasmDbApi(dbConfig);

            host = builder.Build();

            logger = host.Services
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("AppStartup");

            var langService =
                host.Services.GetRequiredService<ILanguageService>();

            if (langService is JsonLanguageService jsonLang)
            {
                await jsonLang.InitializeAsync(
                    typeof(JsonLanguageService).Assembly);
            }

            var configService =
                host.Services.GetRequiredService<IAppConfigService>();

            await host.Services
                .GetRequiredService<AppConfigService>()
                .LoadAsync();

            var config = configService.Current;

            if (config is not null &&
                !string.IsNullOrWhiteSpace(config.DefaultLanguage))
            {
                langService.SetLanguage(config.DefaultLanguage);
            }

            await ValidateStartupAsync(
                host.Services,
                logger);

            logger.LogInformation(
                "Web application started. SessionId={SessionId} DurationMs={DurationMs}",
                DiagnosticContext.SessionId,
                Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Startup failed");

            Console.Error.WriteLine(
                $"[MAP Startup Error] SessionId={DiagnosticContext.SessionId}");

            Console.Error.WriteLine(ex);

            return;
        }

        await host.RunAsync();
    }

    private static async Task ValidateStartupAsync(
        IServiceProvider services,
        ILogger logger)
    {
        var menuService =
            services.GetRequiredService<IMenuService>();

        await menuService.LoadMenusAsync();

        // Find first navigable page using menu order
        var item = MAP.C.Contract.Menus.MenuTree.FindFirstPage(menuService.Menus)
            ?? throw new InvalidOperationException(
                "Menu does not contain any navigable page.");

        if (!item.IsPage)
            throw new InvalidOperationException(
                $"Startup page '{item.Id}' is not a page.");

        await services
            .GetRequiredService<IModuleLoader>()
            .LoadComponentAsync(item);

        logger.LogInformation(
            "Startup page validated. PageId={PageId}",
            item.Id);
    }
}
