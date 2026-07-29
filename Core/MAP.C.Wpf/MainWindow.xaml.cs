using System.IO;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Components.WebView.Wpf;
using Microsoft.Extensions.Logging;

namespace MAP.C.Wpf;

public partial class MainWindow : Window
{
    public MainWindow(IServiceProvider services, Type rootComponentType)
    {
        var logger = services.GetRequiredService<ILogger<MainWindow>>();
        logger.LogInformation("Creating main window and BlazorWebView.");
        InitializeComponent();
        blazorWebView.Services = services;
        blazorWebView.RootComponents.Add(new RootComponent
        {
            Selector = "#app",
            ComponentType = rootComponentType
        });

        blazorWebView.BlazorWebViewInitializing += (_, e) =>
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var userDataFolder = Path.Combine(appData, "MAP", "WebView2");
            Directory.CreateDirectory(userDataFolder);
            e.EnvironmentOptions = new CoreWebView2EnvironmentOptions();
            e.UserDataFolder = userDataFolder;
            logger.LogInformation("Initializing WebView2. UserDataFolder={UserDataFolder}", userDataFolder);
        };

        blazorWebView.BlazorWebViewInitialized += (_, e) =>
        {
            logger.LogInformation("WebView2 initialized. BrowserVersion={BrowserVersion}", e.WebView.CoreWebView2.Environment.BrowserVersionString);
            e.WebView.CoreWebView2.ProcessFailed += (_, failure) =>
                logger.LogError("WebView2 process failed. Kind={Kind} Reason={Reason}", failure.ProcessFailedKind, failure.Reason);
        };

#if DEBUG
        blazorWebView.BlazorWebViewInitialized += (_, e) =>
        {
            var settings = e.WebView.CoreWebView2.Settings;
            settings.AreDevToolsEnabled = true;
            settings.AreDefaultContextMenusEnabled = true;
        };
#endif
    }
}
