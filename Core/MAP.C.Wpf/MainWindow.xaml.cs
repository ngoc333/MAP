using System.IO;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Components.WebView.Wpf;

namespace MAP.C.Wpf;

public partial class MainWindow : Window
{
    public MainWindow(IServiceProvider services, Type rootComponentType)
    {
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
