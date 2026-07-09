using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace MESInsight
{
    internal static class WindowResizer
    {
        private const int WM_NCLBUTTONDOWN = 0xA1;

        private const int HT_LEFT        = 10;
        private const int HT_RIGHT       = 11;
        private const int HT_TOP         = 12;
        private const int HT_TOPLEFT     = 13;
        private const int HT_TOPRIGHT    = 14;
        private const int HT_BOTTOM      = 15;
        private const int HT_BOTTOMLEFT  = 16;
        private const int HT_BOTTOMRIGHT = 17;

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        public static void DragMove(Window window)
        {
            ReleaseCapture();
            SendMessage(new WindowInteropHelper(window).Handle, WM_NCLBUTTONDOWN, new IntPtr(2), IntPtr.Zero);
        }

        public static void ResizeLeft(Window window)        => Resize(window, HT_LEFT);
        public static void ResizeRight(Window window)       => Resize(window, HT_RIGHT);
        public static void ResizeTop(Window window)         => Resize(window, HT_TOP);
        public static void ResizeBottom(Window window)      => Resize(window, HT_BOTTOM);
        public static void ResizeTopLeft(Window window)     => Resize(window, HT_TOPLEFT);
        public static void ResizeTopRight(Window window)    => Resize(window, HT_TOPRIGHT);
        public static void ResizeBottomLeft(Window window)  => Resize(window, HT_BOTTOMLEFT);
        public static void ResizeBottomRight(Window window) => Resize(window, HT_BOTTOMRIGHT);

        private static void Resize(Window window, int direction)
        {
            ReleaseCapture();
            SendMessage(new WindowInteropHelper(window).Handle, WM_NCLBUTTONDOWN, new IntPtr(direction), IntPtr.Zero);
        }
    }
}