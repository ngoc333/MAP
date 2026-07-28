using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MAP.C.Contract.Models;
using MAP.C.Contract.Menus;

namespace MAP.C.Wpf;

public static class WpfHost
{
    public static void Run(Application application, Type rootComponentType)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(rootComponentType);

        var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services => services.AddWpf(rootComponentType))
            .Build();

        var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("AppStartup");
        logger.LogInformation("Application starting");

        application.DispatcherUnhandledException += (_, e) =>
        {
            logger.LogError(e.Exception, "Unhandled DispatcherException");
            MessageBox.Show(e.Exception.ToString(), "MAP startup error", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        };

        application.Startup += async (_, _) =>
        {
            try
            {
                logger.LogInformation("Host starting");
                await host.StartAsync();
                logger.LogInformation("Host started");

                var menuService = host.Services.GetRequiredService<IMenuService>();
                await menuService.LoadMenusAsync();

                var window = host.Services.GetRequiredService<MainWindow>();
                window.Show();
                window.Activate();
                window.Focus();
                logger.LogInformation("MainWindow shown");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Startup failed");
                MessageBox.Show(ex.ToString(), "MAP startup error", MessageBoxButton.OK, MessageBoxImage.Error);
                application.Shutdown(1);
            }
        };

        application.Exit += async (_, _) =>
        {
            logger.LogInformation("Application shutting down");
            await host.StopAsync();
            host.Dispose();
            logger.LogInformation("Application stopped");
        };
    }
}
