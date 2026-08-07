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
using MAP.C.Runtime.Logging;
using MAP.C.Wpf.Logging;
using MAP.C.Wpf.Menus;
using MAP.C.Wpf.Modules;
using MAP.C.Wpf.Config;
using MAP.C.Runtime.Database;
using MAP.C.Runtime.Navigation;
using MAP.C.Runtime.Config;
using MAP.C.Runtime.Localization;
using MAP.C.UI.Errors;
using Radzen;

namespace MAP.C.Wpf;

internal static class WpfServices
{
    internal static IServiceCollection AddWpf(this IServiceCollection services, Type rootComponentType)
    {
        services.AddWpfBlazorWebView();

        var baseDir = AppContext.BaseDirectory;

        // Register services (no I/O here)
        services.AddSingleton<IResourceLoader, ResourceLoader>();
        services.AddSingleton<ILanguageService>(sp =>
        {
            var loader = sp.GetRequiredService<IResourceLoader>();
            var lang = new JsonLanguageService(loader);
            return lang;
        });
        services.AddLogging(logging =>
        {
            logging.ClearProviders();
            logging.SetMinimumLevel(LogLevel.Information);
            logging.Services.AddSingleton<FileLogStore>();
            logging.Services.AddSingleton<ILoggerProvider, LogStoreLoggerProvider>();
        });

        services.AddSingleton<IModuleLoader>(sp => new ModuleLoader(
            Path.Combine(baseDir, "modules"),
            sp.GetRequiredService<ILanguageService>(),
            sp.GetRequiredService<IResourceLoader>(),
            sp.GetRequiredService<ILogger<ModuleLoader>>()));
        services.AddSingleton<ILogStore>(sp => sp.GetRequiredService<FileLogStore>());
        services.AddSingleton<IPageNavigator, PageNavigator>();
        services.AddSingleton<IAppConfigService>(_ => new AppConfigService(
            Path.Combine(baseDir, "app-config.json")));
        services.AddSingleton<IPlatformCapabilities, PlatformCapabilities>();
        services.AddSingleton<IDbApiClient>(sp =>
        {
            var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("DbApiConfig");
            var configPath = Path.Combine(baseDir, "db-api.json");
            try
            {
                var config = DbApiConfiguration.LoadFromFile(configPath);
                return new DbApiClient(new HttpClient
                {
                    BaseAddress = config.OracleBaseAddress,
                    Timeout = TimeSpan.FromSeconds(10)
                }, new HttpClient
                {
                    BaseAddress = config.PostgreSqlBaseAddress,
                    Timeout = TimeSpan.FromSeconds(10)
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to load db-api.json. Path={Path}", configPath);
                throw;
            }
        });
        services.AddSingleton<IUiStateService, UiStateService>();
        services.AddSingleton<IMenuService>(sp => new MenuService(
            sp.GetRequiredService<IDbApiClient>(),
            sp.GetRequiredService<IAppConfigService>(),
            sp.GetRequiredService<ILogger<MenuService>>()));
        services.AddSingleton(sp => new MainWindow(sp, rootComponentType));
        services.AddScoped<ModuleErrorNotifier>();

        return services;
    }
}
