using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace RunApp;

public partial class MainWindow : Window
{
    private const string ServerPath = @"\\172.30.10.8\WebService\LGMES_LIVE_6_Service\DeployAssembly\FormAssembly\MAP\desktop";
    private const string AppExeName = "MAP.H.Desktop.exe";

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        var service = new FileSyncService(ServerPath, GetLocalPath());

        service.ProgressChanged += (currentFile, percent, current, total) =>
        {
            Dispatcher.Invoke(() =>
            {
                ProgressFill.Width = ProgressBarGrid.ActualWidth * percent / 100.0;
                PercentText.Text = $"{percent}%";
                StatusText.Text = $"Syncing: {currentFile}";
            });
        };

        service.FileSkipped += (currentFile, percent, current, total) =>
        {
            Dispatcher.Invoke(() =>
            {
                ProgressFill.Width = ProgressBarGrid.ActualWidth * percent / 100.0;
                PercentText.Text = $"{percent}%";
                StatusText.Text = $"Skip: {currentFile}";
            });
        };

        try
        {
            var result = await Task.Run(() => service.SyncAsync());

            if (result.TotalFiles == 0)
            {
                StatusText.Text = "No files found on server.";
                return;
            }

            StatusText.Text = "Launching MAP...";
            await Task.Delay(300);

            var localAppPath = Path.Combine(GetLocalPath(), AppExeName);
            if (File.Exists(localAppPath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = localAppPath,
                    WorkingDirectory = Path.GetDirectoryName(localAppPath)
                });
            }
            else
            {
                MessageBox.Show($"Application not found:\n{localAppPath}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error: {ex.Message}";
        }
    }

    private static string GetLocalPath()
    {
        return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "desktop");
    }
}
