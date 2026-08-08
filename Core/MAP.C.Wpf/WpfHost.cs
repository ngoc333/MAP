using System.IO;
using System.Text.Json;
using System.Diagnostics;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MAP.C.Contract.Config;
using MAP.C.Contract.Localization;
using MAP.C.Contract.Models;
using MAP.C.Contract.Menus;
using MAP.C.Contract.Modules;
using MAP.C.Contract.Logging;
using MAP.C.Runtime.Localization;
using MAP.C.UI.Layout;

namespace MAP.C.Wpf;

public static class WpfHost
{
    public static void Run(Application application)
    {
        ArgumentNullException.ThrowIfNull(application);

        var rootComponentType = typeof(MainLayout);
        var started = Stopwatch.GetTimestamp();
        var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddWpf(rootComponentType);
            })
            .Build();

        var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("AppStartup");
        logger.LogInformation("Application starting. SessionId={SessionId} BaseDirectory={BaseDirectory} CurrentDirectory={CurrentDirectory} ProcessId={ProcessId} Framework={Framework} OS={OS}",
            DiagnosticContext.SessionId, AppContext.BaseDirectory, Environment.CurrentDirectory,
            Environment.ProcessId, System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            System.Runtime.InteropServices.RuntimeInformation.OSDescription);

        application.DispatcherUnhandledException += (_, e) =>
        {
            logger.LogError(e.Exception, "Unhandled DispatcherException");
            // Default: do NOT handle unknown exceptions - let normal process failure behavior apply
            // Module-level faults are handled by ModuleErrorBoundary, not here
            e.Handled = false;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            logger.LogCritical(e.ExceptionObject as Exception, "Unhandled AppDomain exception. IsTerminating={IsTerminating}", e.IsTerminating);
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            logger.LogError(e.Exception, "Unobserved task exception");
            e.SetObserved();
        };

        application.Startup += async (_, _) =>
        {
            try
            {
                logger.LogInformation("Host starting");
                await host.StartAsync();
                logger.LogInformation("Host started. DurationMs={DurationMs}", Stopwatch.GetElapsedTime(started).TotalMilliseconds);

                // Initialize localization
                var langService = host.Services.GetRequiredService<ILanguageService>();
                if (langService is JsonLanguageService jsonLang)
                    await jsonLang.InitializeAsync(typeof(JsonLanguageService).Assembly);

                // Load app configuration (may be null on first run)
                var configService = host.Services.GetRequiredService<IAppConfigService>();
                var config = configService.Current;

                // Configure window position/size
                var window = host.Services.GetRequiredService<MainWindow>();

                if (config != null)
                {
                    if (config.Fullscreen)
                    {
                        DisplayHelper.FullscreenOnDisplay(window, config.DisplayIndex, config.HideTaskbar);
                    }
                    else if (config.DisplayIndex > 0)
                    {
                        var displays = configService.GetDisplays();
                        if (config.DisplayIndex < displays.Count)
                            DisplayHelper.PositionOnDisplay(window, config.DisplayIndex);
                    }
                    else
                    {
                        window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                    }

                    if (!string.IsNullOrWhiteSpace(config.DefaultLanguage))
                    {
                        langService.SetLanguage(config.DefaultLanguage);
                    }
                }
                else
                {
                    window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                }

                // Validate all required startup resources before showing the window
                await ValidateStartupAsync(host.Services, logger);

                window.Show();
                window.Activate();
                window.Focus();
                logger.LogInformation("MainWindow shown. StartupDurationMs={DurationMs}", Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Startup failed");
                MessageBox.Show(
                    BuildStartupErrorMessage(ex),
                    "MAP Startup Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                application.Shutdown(1);
            }
        };

        application.Exit += (_, _) =>
        {
            logger.LogInformation("Application shutting down. SessionId={SessionId}", DiagnosticContext.SessionId);
            try
            {
                host.StopAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during shutdown");
            }
            finally
            {
                host.Dispose();
                logger.LogInformation("Application stopped");
            }
        };
    }

    /// <summary>
    /// Validates all required startup resources before showing the main window.
    /// Throws if any required resource is missing or invalid.
    /// </summary>
    private static async Task ValidateStartupAsync(IServiceProvider services, ILogger logger)
    {
        // 1. Validate menu configuration loads successfully
        var menuService = services.GetRequiredService<IMenuService>();
        await menuService.LoadMenusAsync();
        logger.LogInformation("Startup validation: menu loaded. MenuCount={MenuCount}", menuService.Menus.Count);

        // 2. Validate default page if configured
        var configService = services.GetRequiredService<IAppConfigService>();
        var config = configService.Current;
        if (config?.DefaultPageId is { Length: > 0 } defaultPageId)
        {
            var menuItem = menuService.FindById(defaultPageId)
                ?? throw new InvalidOperationException(
                    $"Configured default page '{defaultPageId}' was not found in menu configuration.");

            if (menuItem.IsPage)
            {
                var moduleLoader = services.GetRequiredService<IModuleLoader>();
                await moduleLoader.LoadComponentAsync(menuItem);
                logger.LogInformation("Startup validation: default page loaded. PageId={PageId} Assembly={Assembly} Component={Component}",
                    defaultPageId, menuItem.Assembly, menuItem.Component);
            }
        }

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
