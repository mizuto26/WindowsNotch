using System.Windows.Media;

namespace WindowsNotch.App.Models;

public sealed class ShelfItem
{
    public required string DisplayName { get; init; }

    public required string StoredPath { get; init; }

    public bool IsDirectory { get; init; }

    public DateTime AddedAtUtc { get; init; }

    public ImageSource? ThumbnailSource { get; init; }

    public string KindLabel => IsDirectory ? "DIR" : "FILE";
}
