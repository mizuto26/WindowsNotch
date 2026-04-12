using System.IO;

namespace WindowsNotch.App.Services;

public sealed class FileDropService
{
    private readonly ICloudDriveLocator _iCloudDriveLocator;

    public FileDropService(ICloudDriveLocator iCloudDriveLocator)
    {
        _iCloudDriveLocator = iCloudDriveLocator;
    }

    public Task<CopyResult> CopyEntriesAsync(IEnumerable<string> entryPaths, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            try
            {
                if (!_iCloudDriveLocator.TryResolveWindowsNotchFolder(out var destinationFolder))
                {
                    return new CopyResult(
                        false,
                        "iCloud Drive was not found. Turn on iCloud Drive in iCloud for Windows first.",
                        null,
                        0);
                }

                Directory.CreateDirectory(destinationFolder);

                var copiedCount = 0;

                foreach (var entryPath in entryPaths)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (File.Exists(entryPath) || Directory.Exists(entryPath))
                    {
                        StorageCopyHelper.CopyEntryToUniqueDestination(entryPath, destinationFolder);
                        copiedCount++;
                    }
                }

                if (copiedCount == 0)
                {
                    return new CopyResult(
                        false,
                        "No files or folders were found in the drop payload.",
                        destinationFolder,
                        0);
                }

                var message = copiedCount == 1
                    ? "Copied 1 item into iCloud Drive."
                    : $"Copied {copiedCount} items into iCloud Drive.";

                return new CopyResult(true, message, destinationFolder, copiedCount);
            }
            catch (OperationCanceledException)
            {
                return new CopyResult(
                    false,
                    "Copy was canceled.",
                    null,
                    0);
            }
            catch (Exception ex)
            {
                return new CopyResult(
                    false,
                    $"Copy failed: {ex.Message}",
                    null,
                    0);
            }
        }, cancellationToken);
    }

}
