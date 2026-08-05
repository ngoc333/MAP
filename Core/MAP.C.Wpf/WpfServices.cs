using System.IO;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Components.WebView.Wpf;
using MAP.C.Contract.Localization;
using MAP.C.Contract.Navigation;
using MAP.C.Contract.Menus;
using MAP.C.Contract.Modules;
using MAP.C.Contract.Database;
using MAP.C.Contract.Logging;
using MAP.C.Contract.Config;
using MAP.C.Wpf.Logging;
using MAP.C.Wpf.Menus;
using MAP.C.Wpf.Modules;
using MAP.C.Wpf.Config;
using MAP.C.Runtime.Database;
using MAP.C.Runtime.Navigation;
using MAP.C.Runtime.Config;
using MAP.C.Runtime.Localization;

namespace MAP.C.Wpf;

internal static class WpfServices
{
    internal static IServiceCollection AddWpf(this IServiceCollection services, Type rootComponentType)
    {
        services.AddWpfBlazorWebView();

        var loader = new ResourceLoader();
        var langService = new JsonLanguageService(loader);
        langService.InitializeAsync(typeof(JsonLanguageService).Assembly).GetAwaiter().GetResult();

        services.AddSingleton<IResourceLoader>(loader);
        services.AddSingleton<ILanguageService>(langService);
        services.AddLogging(logging =>
        {
            logging.ClearProviders();
            logging.SetMinimumLevel(LogLevel.Information);
            logging.Services.AddSingleton<FileLogStore>();
            logging.Services.AddSingleton<ILoggerProvider, FileLoggerProvider>();
        });

        var baseDir = AppContext.BaseDirectory;

        services.AddSingleton<IModuleLoader>(sp => new ModuleLoader(
            Path.Combine(baseDir, "modules"), langService, sp.GetRequiredService<IResourceLoader>(), sp.GetRequiredService<ILogger<ModuleLoader>>()));
        services.AddSingleton<ILogStore>(sp => sp.GetRequiredService<FileLogStore>());
        services.AddSingleton<IPageNavigator, PageNavigator>();
        services.AddSingleton<IAppConfigService>(_ => new AppConfigService(
            Path.Combine(baseDir, "app-config.json")));
        services.AddSingleton<IPlatformCapabilities, PlatformCapabilities>();
        var dbApiConfiguration = DbApiConfiguration.LoadFromFile(Path.Combine(baseDir, "db-api.json"));
        services.AddSingleton<IDbApiClient>(_ => new DbApiClient(new HttpClient
        {
            BaseAddress = dbApiConfiguration.OracleBaseAddress,
            Timeout = TimeSpan.FromSeconds(10)
        }, new HttpClient
        {
            BaseAddress = dbApiConfiguration.PostgreSqlBaseAddress,
            Timeout = TimeSpan.FromSeconds(10)
        }));
        services.AddSingleton<IUiStateService, UiStateService>();
services.AddSingleton<IMenuService>(sp => new MenuService(
            sp.GetRequiredService<IDbApiClient>(), sp.GetRequiredService<IAppConfigService>(), sp.GetRequiredService<ILogger<MenuService>>()));
        services.AddSingleton(sp => new MainWindow(sp, rootComponentType));

        return services;
    }
}
