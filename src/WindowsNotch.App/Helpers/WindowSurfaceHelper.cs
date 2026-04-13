using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace WindowsNotch.App;

internal static class WindowSurfaceHelper
{
    internal static void UpdateClip(FrameworkElement surface, double cornerRadius)
    {
        if (surface.ActualWidth <= 0 || surface.ActualHeight <= 0)
        {
            return;
        }

        surface.Clip = new RectangleGeometry(
            new Rect(0, 0, surface.ActualWidth, surface.ActualHeight),
            cornerRadius,
            cornerRadius);
    }

    internal static void HandleWindowDragMove(Window window, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        window.DragMove();
    }
}
