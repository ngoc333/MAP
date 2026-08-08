using System.Runtime.InteropServices;
using System.Windows;
using MAP.C.Contract.Models;

namespace MAP.C.Wpf;

internal static class DisplayHelper
{
    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumDelegate lpfnEnum, IntPtr dwData);

    private delegate bool MonitorEnumDelegate(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
        public readonly int Width => Right - Left;
        public readonly int Height => Bottom - Top;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    /// <summary>
    /// Returns all connected displays with their index, name, and primary status.
    /// This is the single source of truth for display enumeration.
    /// </summary>
    public static IReadOnlyList<DisplayInfo> GetDisplays()
    {
        var displays = new List<DisplayInfo>();
        var index = 0;

        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr hdc, ref RECT rc, IntPtr lParam) =>
        {
            var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (GetMonitorInfo(hMonitor, ref mi))
            {
                var isPrimary = (mi.dwFlags & 1) != 0; // MONITORINFOF_PRIMARY
                var name = $"Display {index + 1}{(isPrimary ? " (Primary)" : "")}";
                displays.Add(new DisplayInfo(index, name, isPrimary));
            }
            index++;
            return true;
        }, IntPtr.Zero);

        return displays;
    }

    public static void PositionOnDisplay(Window window, int displayIndex)
    {
        var work = GetMonitorWorkArea(displayIndex);
        if (work == null) return;

        window.Left = work.Value.Left;
        window.Top = work.Value.Top;
        window.WindowStartupLocation = WindowStartupLocation.Manual;
    }

    public static void FullscreenOnDisplay(Window window, int displayIndex, bool hideTaskbar)
    {
        var rect = hideTaskbar ? GetMonitorBounds(displayIndex) : GetMonitorWorkArea(displayIndex);
        if (rect == null) return;

        window.WindowState = WindowState.Normal;
        window.Left = rect.Value.Left;
        window.Top = rect.Value.Top;
        window.Width = rect.Value.Width;
        window.Height = rect.Value.Height;
        window.WindowStyle = WindowStyle.None;
        window.ResizeMode = ResizeMode.NoResize;
        window.WindowStartupLocation = WindowStartupLocation.Manual;
    }

    private static MONITORINFO? GetMonitorInfoForIndex(int displayIndex)
    {
        var index = 0;
        MONITORINFO? target = null;

        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr hdc, ref RECT rc, IntPtr lParam) =>
        {
            if (index == displayIndex)
            {
                var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
                GetMonitorInfo(hMonitor, ref mi);
                target = mi;
            }
            index++;
            return true;
        }, IntPtr.Zero);

        return target;
    }

    private static RECT? GetMonitorWorkArea(int displayIndex)
    {
        return GetMonitorInfoForIndex(displayIndex)?.rcWork;
    }

    private static RECT? GetMonitorBounds(int displayIndex)
    {
        return GetMonitorInfoForIndex(displayIndex)?.rcMonitor;
    }
}
