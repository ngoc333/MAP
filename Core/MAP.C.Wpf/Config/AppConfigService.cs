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
                if (Exists)
                {
                    var json = File.ReadAllText(_configPath);
                    _current = JsonSerializer.Deserialize<AppConfig>(json,
                        new JsonSerializerOptions(JsonSerializerDefaults.Web))
                        ?? new AppConfig();
                }
            }
            return _current;
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
            // DISPLAY_DEVICE_ATTACHED_TO_DESKTOP = 0x00000001
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
        await File.WriteAllTextAsync(_configPath, json);
        _current = config;
    }

    public void RestartApp()
    {
        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(processPath))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = processPath,
                UseShellExecute = true
            });
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
