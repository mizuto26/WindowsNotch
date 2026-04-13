using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using WindowsNotch.App.Models;
using WindowsNotch.App.Services;

namespace WindowsNotch.App;

public partial class SettingsWindow : Window
{
    private readonly ICloudDriveLocator _iCloudDriveLocator;

    public SettingsWindow(AppSettings settings, ICloudDriveLocator iCloudDriveLocator)
    {
        InitializeComponent();

        _iCloudDriveLocator = iCloudDriveLocator;
        LaunchAtStartupCheckBox.IsChecked = settings.LaunchAtStartup;
    }

    public bool ShouldExitAfterClose { get; private set; }

    public AppSettings ResultSettings =>
        new()
        {
            LaunchAtStartup = LaunchAtStartupCheckBox.IsChecked == true,
        };

    private void OpenShareGuideButton_Click(object sender, RoutedEventArgs e)
    {
        var folderText = _iCloudDriveLocator.TryResolveWindowsNotchFolder(out var folderPath)
            ? $"Shared folder\n{folderPath}\n\nThis app currently shares through iCloud Drive, not native AirDrop."
            : "Shared folder\niCloud Drive\\WindowsNotch\n\nTurn on iCloud Drive in iCloud for Windows first. This app currently shares through iCloud Drive, not native AirDrop.";

        var guideWindow = new ShareGuideWindow(folderText)
        {
            Owner = this,
        };

        guideWindow.ShowDialog();
    }

    private void OpenICloudFolderButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_iCloudDriveLocator.TryResolveWindowsNotchFolder(out var folderPath))
        {
            MessageBox.Show(
                this,
                "iCloud Drive was not found. Turn it on in iCloud for Windows first.",
                "WindowsNotch",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        Directory.CreateDirectory(folderPath);

        Process.Start(new ProcessStartInfo
        {
            FileName = folderPath,
            UseShellExecute = true,
        });
    }

    private void CloseChromeButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void ExitButton_Click(object sender, RoutedEventArgs e)
    {
        ShouldExitAfterClose = true;
        DialogResult = true;
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }
}
