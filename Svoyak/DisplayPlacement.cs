using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Svoyak;

/// <summary>Places the audience surface on a monitor other than the host's monitor.</summary>
internal static class DisplayPlacement
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct ScreenBounds
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    private delegate bool MonitorCallback(IntPtr monitor, IntPtr device, ref ScreenBounds bounds, IntPtr data);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr device, IntPtr clip, MonitorCallback callback, IntPtr data);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

    public static ScreenBounds? FindAudienceScreen(Window host)
    {
        IntPtr hostMonitor = MonitorFromWindow(new WindowInteropHelper(host).Handle, 2);
        ScreenBounds? audience = null;
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
            (IntPtr monitor, IntPtr device, ref ScreenBounds bounds, IntPtr data) =>
            {
                if (monitor != hostMonitor && audience == null) audience = bounds;
                return true;
            }, IntPtr.Zero);
        return audience;
    }

    public static void ShowOnScreen(Window window, ScreenBounds bounds)
    {
        // Native monitor bounds and SetWindowPos both use physical pixels, including mixed DPI.
        var handle = new WindowInteropHelper(window).EnsureHandle();
        SetWindowPos(handle, IntPtr.Zero, bounds.Left, bounds.Top,
            bounds.Right - bounds.Left, bounds.Bottom - bounds.Top, 0x0014);
        window.Show();
        SetWindowPos(handle, IntPtr.Zero, bounds.Left, bounds.Top,
            bounds.Right - bounds.Left, bounds.Bottom - bounds.Top, 0x0014);
    }
}
