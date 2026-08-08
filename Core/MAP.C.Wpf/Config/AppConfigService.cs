using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using MAP.C.Contract.Config;
using MAP.C.Contract.Models;
using Microsoft.Extensions.Logging;

namespace MAP.C.Wpf.Config;

public sealed class AppConfigService : IAppConfigService
{
    private readonly string _configPath;
    private readonly ILogger<AppConfigService> _logger;
    private AppConfig? _current;
    private bool _loaded;

    public AppConfigService(string configPath, ILogger<AppConfigService> logger)
    {
        _configPath = configPath;
        _logger = logger;
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
                _logger.LogDebug("App config loaded from {Path}", _configPath);
            }
            else
            {
                _logger.LogInformation("App config not found at {Path}, using defaults", _configPath);
            }
        }
        catch (JsonException ex)
        {
            // Corrupt config - rename and log
            try
            {
                var corruptPath = $"{_configPath}.corrupt-{DateTime.Now:yyyyMMddHHmmss}";
                File.Move(_configPath, corruptPath);
                _logger.LogWarning(ex, "Corrupt config renamed to {CorruptPath}", corruptPath);
            }
            catch (Exception moveEx)
            {
                _logger.LogError(moveEx, "Failed to rename corrupt config at {Path}", _configPath);
            }
            _current = new AppConfig();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load app config from {Path}", _configPath);
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
            _logger.LogError("Cannot restart: ProcessPath is null or empty");
            MessageBox.Show("Cannot restart: executable path not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                _logger.LogError("Process.Start returned null when restarting");
                MessageBox.Show("Cannot restart: new process was not created.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Restart failed. ProcessPath={ProcessPath}", processPath);
            MessageBox.Show($"Failed to restart:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
