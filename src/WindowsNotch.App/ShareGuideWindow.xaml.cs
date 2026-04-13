using System.Windows;
using System.Windows.Input;

namespace WindowsNotch.App;

public partial class ShareGuideWindow : Window
{
    public ShareGuideWindow(string folderPathText)
    {
        InitializeComponent();
        FolderPathTextBlock.Text = folderPathText;
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
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }
}
