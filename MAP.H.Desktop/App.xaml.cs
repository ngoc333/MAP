using System.Windows;
using MAP.C.Wpf;
using MAP.C.UI.Headers;
using MAP.C.UI.Localization;
using Microsoft.Extensions.DependencyInjection;
using Radzen;

namespace MAP.H.Desktop;

public partial class App : Application
{
    public App() => WpfHost.Run(this, typeof(DesktopApp), services =>
    {
        services.AddSingleton<IPageHeaderState, PageHeaderState>();
        services.AddSingleton<ILocalizer, RadzenLocalizer>();
        services.AddRadzenComponents();
    });
}
