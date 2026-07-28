using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Components.WebView.Wpf;
using MAP.C.Contract.Navigation;
using MAP.C.Contract.Menus;
using MAP.C.Contract.Modules;
using MAP.C.Contract.UI.Headers;
using MAP.C.Runtime.Navigation;
using MAP.C.Runtime.UI.Headers;
using Radzen;

namespace MAP.C.Wpf;

internal static class WpfServices
{
    internal static IServiceCollection AddWpf(this IServiceCollection services, Type rootComponentType)
    {
        services.AddWpfBlazorWebView();
        services.AddRadzenComponents();
        services.AddLogging(logging =>
        {
            logging.ClearProviders();
            logging.SetMinimumLevel(LogLevel.Information);
        });

        var baseDir = AppContext.BaseDirectory;

        services.AddSingleton<IMenuService>(new DesktopMenuService());
        services.AddSingleton<IModuleLoader>(
            sp => new DesktopModuleLoader(Path.Combine(baseDir, "modules")));
        services.AddSingleton<IPageNavigator, PageNavigator>();
        services.AddSingleton<IPageHeaderState, PageHeaderState>();
        services.AddSingleton(sp => new MainWindow(sp, rootComponentType));

        return services;
    }
}
