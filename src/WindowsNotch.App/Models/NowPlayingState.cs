namespace WindowsNotch.App.Models;

public sealed class NowPlayingState
{
    public bool HasSession { get; init; }

    public string Title { get; init; } = "Nothing playing";

    public string Artist { get; init; } = "No media is playing right now.";

    public bool IsPlaying { get; init; }

    public bool CanPlayPause { get; init; }

    public bool CanGoNext { get; init; }

    public bool CanGoPrevious { get; init; }

    public TimeSpan Position { get; init; }

    public TimeSpan Duration { get; init; }

    public DateTime CapturedAtUtc { get; init; } = DateTime.UtcNow;

    public string SourceAppId { get; init; } = string.Empty;

    public byte[]? ArtworkBytes { get; init; }
}
