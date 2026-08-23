using System;
using System.Runtime.InteropServices;

using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;

namespace ImageOrganizer
{
    internal static class DisplayHelper
    {
        public static double? GetRefreshRateForWindow(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero)
                return null;

            var hMon = PInvoke.MonitorFromWindow((HWND)hwnd, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
            if (hMon == IntPtr.Zero)
                return null;

            var monitorInfo = new MONITORINFOEXW();
            monitorInfo.monitorInfo.cbSize = (uint)Marshal.SizeOf<MONITORINFOEXW>();
            if (!PInvoke.GetMonitorInfo(hMon, ref monitorInfo.monitorInfo))
                return null;

            string deviceName = monitorInfo.szDevice.AsReadOnlySpan().ToString();

            var devMode = new DEVMODEW();
            devMode.dmSize = (ushort)Marshal.SizeOf<DEVMODEW>();
            if (!PInvoke.EnumDisplaySettings(deviceName, ENUM_DISPLAY_SETTINGS_MODE.ENUM_CURRENT_SETTINGS, ref devMode))
                return null;

            return devMode.dmDisplayFrequency > 0 ? devMode.dmDisplayFrequency : null;
        }
    }
}