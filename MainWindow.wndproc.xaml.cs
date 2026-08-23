using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;

using Microsoft.Extensions.DependencyInjection;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

using WinRT.Interop;

namespace ImageOrganizer
{
    public sealed partial class MainWindow
    {
        private IntPtr _hwnd;
        private IntPtr _oldWndProc = IntPtr.Zero;
        private WNDPROC? _newWndProc;

        // Install the hook (call from constructor or Loaded)
        private void InstallDpiHook()
        {
            _hwnd = WindowNative.GetWindowHandle(this);

            // Keep delegate alive on the instance
            _newWndProc = CustomWndProc;

            // Replace WndProc and keep old
            _oldWndProc = PInvoke.SetWindowLongPtr((HWND)_hwnd,
                                                   WINDOW_LONG_PTR_INDEX.GWL_WNDPROC,
                                                   Marshal.GetFunctionPointerForDelegate(_newWndProc));
        }

        // Uninstall (call on Closed)
        private void UninstallDpiHook()
        {
            if (_oldWndProc != IntPtr.Zero)
            {
                PInvoke.SetWindowLongPtr((HWND)_hwnd,
                                         WINDOW_LONG_PTR_INDEX.GWL_WNDPROC,
                                         _oldWndProc);
                _oldWndProc = IntPtr.Zero;
                _newWndProc = null;
            }
        }

        private LRESULT CustomWndProc(HWND hWnd, uint msg, WPARAM wParam, LPARAM lParam)
        {
            // Use IMessenger to broadcast changes
            var messenger = App.Current.Services.GetService<IMessenger>();

            if (msg == PInvoke.WM_DPICHANGED)
            {
                // LOWORD = dpiX, HIWORD = dpiY
                nuint w = wParam.Value;
                int dpiX = (int)(w & 0xffff);
                int dpiY = (int)((w >> 16) & 0xffff);

                messenger?.Send(new ValueChangedMessage<(int dpiX, int dpiY)>((dpiX, dpiY)));
            }
            else if (msg == PInvoke.WM_DISPLAYCHANGE)
            {
                double? refreshHz = null;
                try
                {
                    refreshHz = DisplayHelper.GetRefreshRateForWindow(hWnd);
                }
                catch
                {
                    // Ignore
                }

                messenger?.Send(new ValueChangedMessage<double?>(refreshHz));
            }

            // Forward to original wndproc
            return PInvoke.CallWindowProc(Marshal.GetDelegateForFunctionPointer<WNDPROC>(_oldWndProc),
                                          hWnd, msg, wParam, lParam);
        }
    }
}