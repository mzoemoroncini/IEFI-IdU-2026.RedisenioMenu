using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Mesa.Desktop;

public static class WindowBounds
{
    // RECT and SetWindowPos use physical pixels in this PerMonitorV2 application.
    // Keeping this calculation in pixels avoids mixing WPF units with Windows scale.
    public static Rect FitInWorkArea(Rect workArea, Rect requested, double dpiScale, bool center = true)
    {
        if (workArea.Width <= 0 || workArea.Height <= 0 || !double.IsFinite(dpiScale) || dpiScale <= 0)
            throw new ArgumentOutOfRangeException(nameof(workArea));
        double gap = Math.Min(12 * dpiScale, Math.Min(workArea.Width, workArea.Height) / 8);
        var available = new Rect(workArea.Left + gap, workArea.Top + gap,
            workArea.Width - gap * 2, workArea.Height - gap * 2);
        double width = Math.Min(requested.Width, available.Width);
        double height = Math.Min(requested.Height, available.Height);
        double left = center ? available.Left + (available.Width - width) / 2 : Math.Clamp(requested.Left, available.Left, available.Right - width);
        double top = center ? available.Top + (available.Height - height) / 2 : Math.Clamp(requested.Top, available.Top, available.Bottom - height);
        return new Rect(left, top, width, height);
    }

    public static void EnsureVisible(Window window, bool center = true)
    {
        if (!OperatingSystem.IsWindows() || window.WindowState != WindowState.Normal) return;
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero || !GetWindowRect(handle, out var bounds)) return;
        var monitor = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(MonitorFromWindow(handle, 2), ref monitor)) return;
        double scale = GetDpiForWindow(handle) / 96d;
        if (scale <= 0) scale = 1;
        var fitted = FitInWorkArea(ToRect(monitor.Work), ToRect(bounds), scale, center);
        // WPF minimums must also fit, otherwise Windows can push the caption off screen.
        window.MinWidth = Math.Min(1120, Math.Floor(fitted.Width / scale));
        window.MinHeight = Math.Min(720, Math.Floor(fitted.Height / scale));
        _ = SetWindowPos(handle, IntPtr.Zero, (int)Math.Ceiling(fitted.Left), (int)Math.Ceiling(fitted.Top),
            (int)Math.Floor(fitted.Width), (int)Math.Floor(fitted.Height), 0x0004 | 0x0010);
    }

    public static bool IsInsideWorkArea(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        var monitor = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        return GetWindowRect(handle, out var bounds) && GetMonitorInfo(MonitorFromWindow(handle, 2), ref monitor)
            && ToRect(monitor.Work).Contains(ToRect(bounds));
    }

    private static Rect ToRect(NativeRect rect) => new(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo { public int Size; public NativeRect Monitor, Work; public uint Flags; }
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetWindowRect(IntPtr window, out NativeRect rect);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr window);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
}
