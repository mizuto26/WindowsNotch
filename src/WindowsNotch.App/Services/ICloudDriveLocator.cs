using System.IO;

namespace WindowsNotch.App.Services;

public sealed class ICloudDriveLocator
{
    private static readonly string[] CandidateFolderNames =
    [
        "iCloudDrive",
        "iCloud Drive",
    ];

    public bool TryResolveICloudDriveRoot(out string rootPath)
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        foreach (var folderName in CandidateFolderNames)
        {
            var candidate = Path.Combine(userProfile, folderName);
            if (Directory.Exists(candidate))
            {
                rootPath = candidate;
                return true;
            }
        }

        rootPath = string.Empty;
        return false;
    }

    public bool TryResolveWindowsNotchFolder(out string folderPath)
    {
        if (!TryResolveICloudDriveRoot(out var rootPath))
        {
            folderPath = string.Empty;
            return false;
        }

        folderPath = Path.Combine(rootPath, "WindowsNotch");
        return true;
    }
}
