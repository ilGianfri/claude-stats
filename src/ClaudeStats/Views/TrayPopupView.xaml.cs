using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;

namespace ClaudeStats.Views;

/// <summary>
/// Compact popup panel shown from the tray icon, listing each limit window with its usage and reset.
/// </summary>
public partial class TrayPopupView : UserControl
{
    private bool _isDragging;
    private Point _dragStartScreenPos;
    private IntPtr _hwnd;

    /// <summary>Initializes the popup view.</summary>
    public TrayPopupView()
    {
        InitializeComponent();
    }

    private void DragHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        HwndSource? source = PresentationSource.FromVisual(this) as HwndSource;
        if (source is null) return;

        _hwnd = source.Handle;
        _dragStartScreenPos = PointToScreen(e.GetPosition(this));
        _isDragging = true;
        ((UIElement)sender).CaptureMouse();
        e.Handled = true;
    }

    private void DragHandle_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging) return;

        Point current = PointToScreen(e.GetPosition(this));
        int dx = (int)(current.X - _dragStartScreenPos.X);
        int dy = (int)(current.Y - _dragStartScreenPos.Y);

        NativeMethods.GetWindowRect(_hwnd, out NativeMethods.RECT rect);
        NativeMethods.SetWindowPos(
            _hwnd, IntPtr.Zero,
            rect.Left + dx, rect.Top + dy,
            0, 0,
            NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);

        _dragStartScreenPos = current;
    }

    private void DragHandle_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging) return;
        _isDragging = false;
        ((UIElement)sender).ReleaseMouseCapture();
    }

    private void ClosePopup_Click(object sender, RoutedEventArgs e)
    {
        DependencyObject? obj = this;
        while (obj is not null)
        {
            obj = LogicalTreeHelper.GetParent(obj);
            if (obj is Popup popup)
            {
                popup.IsOpen = false;
                return;
            }
        }
    }

    private static class NativeMethods
    {
        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_NOZORDER = 0x0004;
        public const uint SWP_NOACTIVATE = 0x0010;

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT { public int Left, Top, Right, Bottom; }

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int X, int Y, int cx, int cy, uint uFlags);
    }
}
