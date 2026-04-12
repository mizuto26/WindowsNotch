using System.IO;

namespace WindowsNotch.App.Services;

internal static class StorageCopyHelper
{
    public static string CopyEntryToUniqueDestination(string sourcePath, string destinationFolder)
    {
        if (File.Exists(sourcePath))
        {
            var destinationPath = GetUniquePath(destinationFolder, Path.GetFileName(sourcePath));
            File.Copy(sourcePath, destinationPath, overwrite: false);
            return destinationPath;
        }

        if (Directory.Exists(sourcePath))
        {
            var destinationPath = GetUniquePath(destinationFolder, Path.GetFileName(sourcePath));
            CopyDirectory(sourcePath, destinationPath);
            return destinationPath;
        }

        throw new FileNotFoundException("The source path does not exist.", sourcePath);
    }

    public static void DeleteEntry(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
            return;
        }

        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    public static string GetUniquePath(string destinationFolder, string fileName)
    {
        var candidatePath = Path.Combine(destinationFolder, fileName);
        if (!File.Exists(candidatePath) && !Directory.Exists(candidatePath))
        {
            return candidatePath;
        }

        var baseName = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        var suffix = 1;

        while (true)
        {
            var nextName = string.IsNullOrEmpty(extension)
                ? $"{baseName} ({suffix})"
                : $"{baseName} ({suffix}){extension}";

            candidatePath = Path.Combine(destinationFolder, nextName);
            if (!File.Exists(candidatePath) && !Directory.Exists(candidatePath))
            {
                return candidatePath;
            }

            suffix++;
        }
    }

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);

        foreach (var filePath in Directory.EnumerateFiles(sourceDirectory))
        {
            var destinationPath = Path.Combine(destinationDirectory, Path.GetFileName(filePath));
            File.Copy(filePath, destinationPath, overwrite: false);
        }

        foreach (var directoryPath in Directory.EnumerateDirectories(sourceDirectory))
        {
            var childDestination = Path.Combine(destinationDirectory, Path.GetFileName(directoryPath));
            CopyDirectory(directoryPath, childDestination);
        }
    }
}
