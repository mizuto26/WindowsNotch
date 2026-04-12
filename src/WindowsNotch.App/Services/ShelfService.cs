using System.Diagnostics;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WindowsNotch.App.Models;

namespace WindowsNotch.App.Services;

public sealed class ShelfService
{
    private readonly ICloudDriveLocator _iCloudDriveLocator;
    private readonly string _shelfRoot;

    public ShelfService(ICloudDriveLocator iCloudDriveLocator)
    {
        _iCloudDriveLocator = iCloudDriveLocator;
        _shelfRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WindowsNotch",
            "Shelf");
    }

    public IReadOnlyList<ShelfItem> LoadItems()
    {
        Directory.CreateDirectory(_shelfRoot);

        return Directory
            .EnumerateFileSystemEntries(_shelfRoot)
            .Select(CreateShelfItem)
            .OrderByDescending(item => item.AddedAtUtc)
            .ToList();
    }

    public Task<IReadOnlyList<ShelfItem>> StashEntriesAsync(IEnumerable<string> entryPaths, CancellationToken cancellationToken = default)
    {
        return Task.Run<IReadOnlyList<ShelfItem>>(() =>
        {
            Directory.CreateDirectory(_shelfRoot);

            foreach (var entryPath in entryPaths)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!File.Exists(entryPath) && !Directory.Exists(entryPath))
                {
                    continue;
                }

                StorageCopyHelper.CopyEntryToUniqueDestination(entryPath, _shelfRoot);
            }

            return LoadItems();
        }, cancellationToken);
    }

    public Task<CopyResult> SendToICloudAsync(ShelfItem item, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            if (!_iCloudDriveLocator.TryResolveWindowsNotchFolder(out var destinationFolder))
            {
                return new CopyResult(
                    false,
                    "iCloud Drive was not found. Turn it on in iCloud for Windows first.",
                    null,
                    0);
            }

            Directory.CreateDirectory(destinationFolder);
            cancellationToken.ThrowIfCancellationRequested();

            StorageCopyHelper.CopyEntryToUniqueDestination(item.StoredPath, destinationFolder);

            return new CopyResult(
                true,
                "Sent to iCloud Drive.",
                destinationFolder,
                1);
        }, cancellationToken);
    }

    public Task<CopyResult> SendEntriesToICloudAsync(IEnumerable<string> entryPaths, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            if (!_iCloudDriveLocator.TryResolveWindowsNotchFolder(out var destinationFolder))
            {
                return new CopyResult(
                    false,
                    "iCloud Drive was not found. Turn it on in iCloud for Windows first.",
                    null,
                    0);
            }

            Directory.CreateDirectory(destinationFolder);
            var copiedCount = 0;

            foreach (var entryPath in entryPaths)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!File.Exists(entryPath) && !Directory.Exists(entryPath))
                {
                    continue;
                }

                StorageCopyHelper.CopyEntryToUniqueDestination(entryPath, destinationFolder);
                copiedCount++;
            }

            if (copiedCount == 0)
            {
                return new CopyResult(
                    false,
                    "No files were available to send.",
                    destinationFolder,
                    0);
            }

            return new CopyResult(
                true,
                copiedCount == 1 ? "Sent to iCloud Drive." : $"Sent {copiedCount} items to iCloud Drive.",
                destinationFolder,
                copiedCount);
        }, cancellationToken);
    }

    public IReadOnlyList<ShelfItem> RemoveItem(ShelfItem item)
    {
        if (File.Exists(item.StoredPath) || Directory.Exists(item.StoredPath))
        {
            StorageCopyHelper.DeleteEntry(item.StoredPath);
        }

        return LoadItems();
    }

    public void OpenItem(ShelfItem item)
    {
        if (!File.Exists(item.StoredPath) && !Directory.Exists(item.StoredPath))
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = item.StoredPath,
            UseShellExecute = true,
        });
    }

    private static ShelfItem CreateShelfItem(string path)
    {
        var isDirectory = Directory.Exists(path);
        var addedAtUtc = isDirectory
            ? Directory.GetLastWriteTimeUtc(path)
            : File.GetLastWriteTimeUtc(path);

        return new ShelfItem
        {
            DisplayName = Path.GetFileName(path),
            StoredPath = path,
            IsDirectory = isDirectory,
            AddedAtUtc = addedAtUtc,
            ThumbnailSource = CreateThumbnailSource(path, isDirectory),
        };
    }

    private static ImageSource? CreateThumbnailSource(string path, bool isDirectory)
    {
        if (isDirectory || !File.Exists(path))
        {
            return null;
        }

        var extension = Path.GetExtension(path).ToLowerInvariant();
        if (extension is not ".png" and not ".jpg" and not ".jpeg" and not ".bmp" and not ".gif")
        {
            return null;
        }

        try
        {
            using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var frame = BitmapFrame.Create(
                fileStream,
                BitmapCreateOptions.IgnoreColorProfile,
                BitmapCacheOption.OnLoad);

            if (frame.PixelWidth <= 144)
            {
                frame.Freeze();
                return frame;
            }

            var scale = 144d / frame.PixelWidth;
            var thumbnail = new TransformedBitmap(frame, new ScaleTransform(scale, scale));
            thumbnail.Freeze();
            return thumbnail;
        }
        catch
        {
            return null;
        }
    }
}
