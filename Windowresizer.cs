using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace MESInsight
{
    internal static class WindowResizer
    {
        private const int WM_NCLBUTTONDOWN = 0xA1;

        private const int HT_LEFT = 10;
        private const int HT_RIGHT = 11;
        private const int HT_TOP = 12;
        private const int HT_TOPLEFT = 13;
        private const int HT_TOPRIGHT = 14;
        private const int HT_BOTTOM = 15;
        private const int HT_BOTTOMLEFT = 16;
        private const int HT_BOTTOMRIGHT = 17;

        private const int MONITOR_DEFAULTTONEAREST = 0x00000002;

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int flags);

        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, MONITORINFO lpmi);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private class MONITORINFO
        {
            public int cbSize = Marshal.SizeOf(typeof(MONITORINFO));
            public RECT rcMonitor;
            public RECT rcWork;
            public int dwFlags;
        }

        public static void DragMove(Window window)
        {
            ReleaseCapture();
            SendMessage(new WindowInteropHelper(window).Handle, WM_NCLBUTTONDOWN, new IntPtr(2), IntPtr.Zero);
        }

        public static void ResizeLeft(Window window) => Resize(window, HT_LEFT);
        public static void ResizeRight(Window window) => Resize(window, HT_RIGHT);
        public static void ResizeTop(Window window) => Resize(window, HT_TOP);
        public static void ResizeBottom(Window window) => Resize(window, HT_BOTTOM);
        public static void ResizeTopLeft(Window window) => Resize(window, HT_TOPLEFT);
        public static void ResizeTopRight(Window window) => Resize(window, HT_TOPRIGHT);
        public static void ResizeBottomLeft(Window window) => Resize(window, HT_BOTTOMLEFT);
        public static void ResizeBottomRight(Window window) => Resize(window, HT_BOTTOMRIGHT);

        private static void Resize(Window window, int direction)
        {
            ReleaseCapture();
            SendMessage(new WindowInteropHelper(window).Handle, WM_NCLBUTTONDOWN, new IntPtr(direction), IntPtr.Zero);
        }

        public static void FitToCurrentMonitor(
            Window window,
            double widthFraction = 0.92, double heightFraction = 0.92,
            double minWidthFraction = 0.6, double minHeightFraction = 0.6,
            double maxWidthCap = double.MaxValue, double maxHeightCap = double.MaxValue)
        {
            IntPtr hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;

            IntPtr monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            if (monitor == IntPtr.Zero) return;

            MONITORINFO monitorInfo = new MONITORINFO();
            if (!GetMonitorInfo(monitor, monitorInfo)) return;

            double scaleX = 1.0;
            double scaleY = 1.0;
            PresentationSource source = PresentationSource.FromVisual(window);
            if (source?.CompositionTarget != null)
            {
                Matrix transform = source.CompositionTarget.TransformFromDevice;
                scaleX = transform.M11;
                scaleY = transform.M22;
            }

            double workWidth = (monitorInfo.rcWork.right - monitorInfo.rcWork.left) * scaleX;
            double workHeight = (monitorInfo.rcWork.bottom - monitorInfo.rcWork.top) * scaleY;

            double desiredWidth = Math.Min(maxWidthCap, workWidth * widthFraction);
            double desiredHeight = Math.Min(maxHeightCap, workHeight * heightFraction);
            double minWidth = Math.Min(maxWidthCap, workWidth * minWidthFraction);
            double minHeight = Math.Min(maxHeightCap, workHeight * minHeightFraction);

            window.MinWidth = minWidth;
            window.MinHeight = minHeight;

            bool tooBig = window.Width > workWidth * 0.98 || window.Height > workHeight * 0.98;
            bool tooSmall = window.Width < minWidth || window.Height < minHeight;

            if (tooBig || tooSmall)
            {
                window.Width = desiredWidth;
                window.Height = desiredHeight;
            }

            double workLeft = monitorInfo.rcWork.left * scaleX;
            double workTop = monitorInfo.rcWork.top * scaleY;

            double clampedLeft = Math.Max(workLeft, Math.Min(window.Left, workLeft + workWidth - window.Width));
            double clampedTop = Math.Max(workTop, Math.Min(window.Top, workTop + workHeight - window.Height));

            if (Math.Abs(clampedLeft - window.Left) > 0.5) window.Left = clampedLeft;
            if (Math.Abs(clampedTop - window.Top) > 0.5) window.Top = clampedTop;
        }

        public static void LockToOwnerMonitor(Window window)
        {
            if (window.Owner == null) return;

            IntPtr ownerHwnd = new WindowInteropHelper(window.Owner).Handle;
            IntPtr selfHwnd = new WindowInteropHelper(window).Handle;
            if (ownerHwnd == IntPtr.Zero || selfHwnd == IntPtr.Zero) return;

            IntPtr ownerMonitor = MonitorFromWindow(ownerHwnd, MONITOR_DEFAULTTONEAREST);
            IntPtr selfMonitor = MonitorFromWindow(selfHwnd, MONITOR_DEFAULTTONEAREST);
            if (ownerMonitor == IntPtr.Zero) return;
            if (ownerMonitor == selfMonitor) return;

            MONITORINFO monitorInfo = new MONITORINFO();
            if (!GetMonitorInfo(ownerMonitor, monitorInfo)) return;

            double scaleX = 1.0;
            double scaleY = 1.0;
            PresentationSource source = PresentationSource.FromVisual(window);
            if (source?.CompositionTarget != null)
            {
                Matrix transform = source.CompositionTarget.TransformFromDevice;
                scaleX = transform.M11;
                scaleY = transform.M22;
            }

            double workLeft = monitorInfo.rcWork.left * scaleX;
            double workTop = monitorInfo.rcWork.top * scaleY;
            double workWidth = (monitorInfo.rcWork.right - monitorInfo.rcWork.left) * scaleX;
            double workHeight = (monitorInfo.rcWork.bottom - monitorInfo.rcWork.top) * scaleY;

            double clampedLeft = Math.Max(workLeft, Math.Min(window.Left, workLeft + workWidth - window.Width));
            double clampedTop = Math.Max(workTop, Math.Min(window.Top, workTop + workHeight - window.Height));

            window.Left = clampedLeft;
            window.Top = clampedTop;
        }
    }
}