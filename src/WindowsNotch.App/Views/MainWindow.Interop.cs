using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;

namespace WindowsNotch.App;

public partial class MainWindow
{
    private Point GetCursorPositionInDeviceIndependentPixels()
    {
        if (!GetCursorPos(out var point))
        {
            return new Point(0, 0);
        }

        var source = PresentationSource.FromVisual(this);
        var transform = source?.CompositionTarget?.TransformFromDevice;

        if (transform is null)
        {
            return new Point(point.X, point.Y);
        }

        return transform.Value.Transform(new Point(point.X, point.Y));
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetCursorPos(out NativePoint point);

    [LibraryImport("user32.dll")]
    private static partial IntPtr GetForegroundWindow();

    [LibraryImport("user32.dll")]
    private static partial IntPtr GetAncestor(IntPtr hWnd, uint gaFlags);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsWindowVisible(IntPtr hWnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsIconic(IntPtr hWnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetWindowRect(IntPtr hWnd, out NativeRect lpRect);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [LibraryImport("user32.dll")]
    private static partial uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmGetWindowAttribute(
        IntPtr hwnd,
        int dwAttribute,
        out NativeRect pvAttribute,
        int cbAttribute);

    [LibraryImport("user32.dll")]
    private static partial IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo lpmi);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int X,
        int Y,
        int cx,
        int cy,
        uint uFlags);

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static partial IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static partial IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static partial int GetWindowLong32(IntPtr hWnd, int nIndex);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static partial int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private static readonly IntPtr HWND_NOTOPMOST = new(-2);
    private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;
    private const uint GA_ROOT = 2;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_SHOWWINDOW = 0x0040;
    private const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MonitorInfo
    {
        public int cbSize;
        public NativeRect Monitor;
        public NativeRect WorkArea;
        public uint Flags;
    }

    private static bool TryGetVisibleWindowRect(IntPtr windowHandle, out NativeRect windowRect)
    {
        if (DwmGetWindowAttribute(
                windowHandle,
                DWMWA_EXTENDED_FRAME_BOUNDS,
                out windowRect,
                Marshal.SizeOf<NativeRect>()) == 0)
        {
            return true;
        }

        return GetWindowRect(windowHandle, out windowRect);
    }

    private static IntPtr GetRootWindowHandle(IntPtr windowHandle)
    {
        if (windowHandle == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        var rootHandle = GetAncestor(windowHandle, GA_ROOT);
        return rootHandle == IntPtr.Zero ? windowHandle : rootHandle;
    }

    private static string GetWindowClassName(IntPtr windowHandle)
    {
        if (windowHandle == IntPtr.Zero)
        {
            return string.Empty;
        }

        var className = new StringBuilder(256);
        return GetClassName(windowHandle, className, className.Capacity) > 0
            ? className.ToString()
            : string.Empty;
    }

    private static bool IsSameProcessWindow(IntPtr windowHandle)
    {
        if (windowHandle == IntPtr.Zero)
        {
            return false;
        }

        var threadId = GetWindowThreadProcessId(windowHandle, out var processId);
        return threadId != 0 && processId == Environment.ProcessId;
    }

    private static bool IsIgnoredOverlayWindowClass(string className)
    {
        return className is "WorkerW" or "Progman" or "Shell_TrayWnd";
    }

    private static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
    {
        return IntPtr.Size == 8
            ? GetWindowLongPtr64(hWnd, nIndex)
            : new IntPtr(GetWindowLong32(hWnd, nIndex));
    }

    private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr newValue)
    {
        return IntPtr.Size == 8
            ? SetWindowLongPtr64(hWnd, nIndex, newValue)
            : new IntPtr(SetWindowLong32(hWnd, nIndex, newValue.ToInt32()));
    }

    private void SetWindowClickThrough(bool isEnabled)
    {
        if (_windowHandle == IntPtr.Zero)
        {
            return;
        }

        var currentStyles = GetWindowLongPtr(_windowHandle, GWL_EXSTYLE).ToInt64();
        var updatedStyles = isEnabled
            ? currentStyles | WS_EX_TRANSPARENT
            : currentStyles & ~WS_EX_TRANSPARENT;

        if (updatedStyles == currentStyles)
        {
            return;
        }

        SetWindowLongPtr(_windowHandle, GWL_EXSTYLE, new IntPtr(updatedStyles));
    }
}
