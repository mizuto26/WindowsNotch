using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace WindowsNotch.App;

public partial class ShareGuideWindow : Window
{
    private const double SurfaceCornerRadius = 20;

    public ShareGuideWindow(string folderPathText)
    {
        InitializeComponent();
        FolderPathTextBlock.Text = folderPathText;
        UpdateWindowClip();
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        DragMove();
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateWindowClip();
    }

    private void UpdateWindowClip()
    {
        if (!IsLoaded || WindowSurfaceBorder.ActualWidth <= 0 || WindowSurfaceBorder.ActualHeight <= 0)
        {
            return;
        }

        WindowSurfaceBorder.Clip = new RectangleGeometry(
            new Rect(0, 0, WindowSurfaceBorder.ActualWidth, WindowSurfaceBorder.ActualHeight),
            SurfaceCornerRadius,
            SurfaceCornerRadius);
    }
}
