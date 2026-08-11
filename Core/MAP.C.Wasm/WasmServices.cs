using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MAP.C.Contract.Config;
using MAP.C.Contract.Localization;
using MAP.C.Contract.Navigation;
using MAP.C.Contract.Menus;
using MAP.C.Contract.Modules;
using MAP.C.Contract.Database;
using MAP.C.Contract.Logging;
using MAP.C.Runtime.Logging;
using MAP.C.Runtime.Database;
using MAP.C.Runtime.Navigation;
using MAP.C.Runtime.Config;
using MAP.C.Runtime.Localization;
using MAP.C.Wasm.Config;
using MAP.C.Wasm.Logging;
using MAP.C.Wasm.Menus;
using MAP.C.Wasm.Modules;
using MAP.C.UI.Headers;
using MAP.C.UI.Localization;
using MAP.C.UI.Errors;
using Radzen;

namespace MAP.C.Wasm;

public static class WasmServices
{
    public static IServiceCollection AddWasm(this IServiceCollection services, HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);

        // Register HttpClient for DI
        services.AddScoped(sp => httpClient);

        // Register Wasm platform services
        services.AddScoped<IMenuService, MenuService>();

        // Register logging (Wasm uses IndexedDB)
        services.AddLogging(logging =>
        {
            logging.ClearProviders();
            logging.SetMinimumLevel(LogLevel.Warning);
            logging.AddFilter("MAP", LogLevel.Information);
#if DEBUG
            logging.AddFilter("MAP", LogLevel.Debug);
#endif
            logging.Services.AddSingleton<IndexedDbLogStore>();
            logging.Services.AddSingleton<ILoggerProvider, LogStoreLoggerProvider>();
        });
        services.AddSingleton<ILogStore>(sp => sp.GetRequiredService<IndexedDbLogStore>());

        // Register Wasm platform implementations
        services.AddSingleton<AppConfigService>();
        services.AddSingleton<IAppConfigService>(sp => sp.GetRequiredService<AppConfigService>());
        services.AddSingleton<IPlatformCapabilities, PlatformCapabilities>();

        // Register common runtime services
        services.AddSingleton<IResourceLoader, ResourceLoader>();
        services.AddSingleton<ILanguageService>(sp =>
        {
            var loader = sp.GetRequiredService<IResourceLoader>();
            return new JsonLanguageService(loader);
        });

        // Register Radzen localizer
        services.AddSingleton<Radzen.ILocalizer, RadzenLocalizer>();

        // Register UI state services
        services.AddScoped<IPageHeaderState, PageHeaderState>();

        // Register module and navigation services
        services.AddScoped<IModuleLoader, ModuleLoader>();
        services.AddScoped<IPageNavigator, PageNavigator>();
        services.AddScoped<IUiStateService, UiStateService>();

        // Register fault notification
        services.AddScoped<ModuleErrorNotifier>();

        // Register Radzen components
        services.AddRadzenComponents();

        return services;
    }

    public static IServiceCollection AddWasmDbApi(this IServiceCollection services, DbApiConfiguration dbApiConfiguration)
    {
        ArgumentNullException.ThrowIfNull(dbApiConfiguration);

        services.AddSingleton<IDbApiClient>(_ => new DbApiClient(new HttpClient
        {
            BaseAddress = dbApiConfiguration.OracleBaseAddress,
            Timeout = TimeSpan.FromSeconds(10)
        }, new HttpClient
        {
            BaseAddress = dbApiConfiguration.PostgreSqlBaseAddress,
            Timeout = TimeSpan.FromSeconds(10)
        }));

        return services;
    }
}
