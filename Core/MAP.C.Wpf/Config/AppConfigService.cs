using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
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

    public IReadOnlyList<DisplayInfo> GetDisplays()
    {
        var displays = new List<DisplayInfo>();
        var dd = new DISPLAY_DEVICE { cb = Marshal.SizeOf<DISPLAY_DEVICE>() };
        uint adapterIndex = 0;
        int displayNum = 1;
        while (EnumDisplayDevices(null, adapterIndex, ref dd, 0))
        {
            if ((dd.StateFlags & 0x00000001) != 0)
            {
                var isPrimary = (dd.StateFlags & 0x00000004) != 0;
                var name = $"Màn hình {displayNum}{(isPrimary ? " (Chính)" : "")}";
                displays.Add(new DisplayInfo(displayNum - 1, name, isPrimary));
                displayNum++;
            }
            adapterIndex++;
        }
        return displays;
    }

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
        if (!string.IsNullOrEmpty(processPath))
        {
            try
            {
                var newProcess = Process.Start(new ProcessStartInfo
                {
                    FileName = processPath,
                    UseShellExecute = true
                });

                if (newProcess is not null)
                {
                    Application.Current.Shutdown();
                    return;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to restart:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
        }
        Application.Current.Shutdown();
    }

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayDevices(string? lpDevice, uint iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private struct DISPLAY_DEVICE
    {
        public int cb;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceString;
        public uint StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceID;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceKey;
    }
}
