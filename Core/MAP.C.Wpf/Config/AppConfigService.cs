using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using MAP.C.Contract.Config;
using MAP.C.Contract.Models;

namespace MAP.C.Wpf.Config;

public sealed class AppConfigService : IAppConfigService
{
    private readonly string _configPath;
    private AppConfig? _current;
    private bool _loaded;

    public AppConfigService(string configPath)
    {
        _configPath = configPath;
    }

    public bool Exists => File.Exists(_configPath);

    public AppConfig? Current
    {
        get
        {
            if (!_loaded)
            {
                _loaded = true;
                LoadConfig();
            }
            return _current;
        }
    }

    private void LoadConfig()
    {
        try
        {
            if (File.Exists(_configPath))
            {
                var json = File.ReadAllText(_configPath);
                _current = JsonSerializer.Deserialize<AppConfig>(json,
                    new JsonSerializerOptions(JsonSerializerDefaults.Web))
                    ?? new AppConfig();
            }
        }
        catch (JsonException ex)
        {
            // Corrupt config - rename and log
            try
            {
                var corruptPath = $"{_configPath}.corrupt-{DateTime.Now:yyyyMMddHHmmss}";
                File.Move(_configPath, corruptPath);
                System.Diagnostics.Debug.WriteLine($"[AppConfigService] Corrupt config renamed to {corruptPath}: {ex.Message}");
            }
            catch { /* best effort */ }
            _current = new AppConfig();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AppConfigService] Failed to load config: {ex.Message}");
            _current = new AppConfig();
        }
    }

    public SystemInfo GetSystemInfo() => new();

    public IReadOnlyList<DisplayInfo> GetDisplays() => DisplayHelper.GetDisplays();

    public async Task SaveAsync(AppConfig config)
    {
        var json = JsonSerializer.Serialize(config,
            new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });

        // Atomic write: temp file → replace
        var tempPath = _configPath + ".tmp";
        await File.WriteAllTextAsync(tempPath, json);

        using (var fs = new FileStream(tempPath, FileMode.Open, FileAccess.Read))
        {
            await fs.FlushAsync();
        }
        File.Move(tempPath, _configPath, overwrite: true);

        _current = config;
    }

    public void RestartApp()
    {
        var processPath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(processPath))
        {
            System.Diagnostics.Debug.WriteLine("[AppConfigService] Cannot restart: ProcessPath is null or empty.");
            MessageBox.Show("Không thể khởi động lại: không tìm thấy đường dẫn executable.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        try
        {
            var newProcess = Process.Start(new ProcessStartInfo
            {
                FileName = processPath,
                UseShellExecute = true
            });

            if (newProcess is null)
            {
                System.Diagnostics.Debug.WriteLine("[AppConfigService] Process.Start returned null.");
                MessageBox.Show("Không thể khởi động lại: process mới không được tạo.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AppConfigService] Restart failed: {ex}");
            MessageBox.Show($"Failed to restart:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
