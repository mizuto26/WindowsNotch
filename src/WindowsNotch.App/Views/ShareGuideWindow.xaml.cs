using System.Windows;
using System.Windows.Input;

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
        WindowSurfaceHelper.HandleWindowDragMove(this, e);
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateWindowClip();
    }

    private void UpdateWindowClip()
    {
        if (!IsLoaded)
        {
            return;
        }

        WindowSurfaceHelper.UpdateClip(WindowSurfaceBorder, SurfaceCornerRadius);
    }
}
