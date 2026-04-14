using System.IO;

namespace WindowsNotch.App.Services;

internal static class AppPaths
{
    private const string AppFolderName = "WindowsNotch";

    internal static string LocalAppDataRoot =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AppFolderName);

    internal static string SettingsFilePath => Path.Combine(LocalAppDataRoot, "settings.json");

    internal static string ShelfRoot => Path.Combine(LocalAppDataRoot, "Shelf");

    internal static string LogRoot => Path.Combine(LocalAppDataRoot, "logs");
}
