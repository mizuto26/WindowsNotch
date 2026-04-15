using System.IO;
using System.Windows.Controls.Primitives;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WindowsNotch.App.Models;

namespace WindowsNotch.App;

public partial class MainWindow
{
    private readonly DispatcherTimer _musicProgressTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(250),
    };

    private NowPlayingState _currentNowPlayingState = new();
    private bool _isMusicProgressDragging;
    private TimeSpan? _pendingSeekPosition;
    private DateTime _pendingSeekUntilUtc;
    private void InitializeMediaUi()
    {
        _musicProgressTimer.Tick += MusicProgressTimer_Tick;
        MusicProgressSlider.AddHandler(Thumb.DragStartedEvent, new DragStartedEventHandler(MusicProgressSlider_DragStarted));
        MusicProgressSlider.AddHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler(MusicProgressSlider_DragCompleted));
    }

    private async Task InitializeMediaSessionAsync()
    {
        try
        {
            await _mediaSessionService.InitializeAsync();
        }
        catch
        {
            ApplyNowPlayingState(new NowPlayingState());
        }
    }

    private void MediaSessionService_StateChanged(object? sender, NowPlayingState state)
    {
        Dispatcher.InvokeAsync(() => ApplyNowPlayingState(state));
    }

    private void ApplyNowPlayingState(NowPlayingState state)
    {
        if (MusicTitleText is null || MusicArtistText is null)
        {
            return;
        }

        state = NormalizeNowPlayingState(state);
        _currentNowPlayingState = state;

        if (_pendingSeekPosition is not null &&
            DateTime.UtcNow >= _pendingSeekUntilUtc)
        {
            _pendingSeekPosition = null;
        }

        MusicTitleText.Text = state.Title;
        MusicArtistText.Text = state.Artist;

        var artwork = CreateArtworkImage(state.ArtworkBytes);
        MusicArtworkShape.Fill = artwork is null
            ? Brushes.Black
            : new ImageBrush(artwork)
            {
                Stretch = Stretch.UniformToFill,
            };
        MusicArtworkFallback.Visibility = artwork is null ? Visibility.Visible : Visibility.Collapsed;

        PreviousTrackButton.IsEnabled = state.CanGoPrevious;
        PlayPauseButton.IsEnabled = state.CanPlayPause;
        NextTrackButton.IsEnabled = state.CanGoNext;
        PlayPauseGlyph.Text = state.IsPlaying ? "\uE769" : "\uE768";
        UpdateMusicProgressPresentation();

        if (state.HasSession && state.Duration > TimeSpan.Zero)
        {
            _musicProgressTimer.Start();
        }
        else
        {
            _musicProgressTimer.Stop();
        }
    }

    private async void PreviousTrackButton_Click(object sender, RoutedEventArgs e)
    {
        await _mediaSessionService.SkipPreviousAsync();
    }

    private async void PlayPauseButton_Click(object sender, RoutedEventArgs e)
    {
        await _mediaSessionService.TogglePlayPauseAsync();
    }

    private async void NextTrackButton_Click(object sender, RoutedEventArgs e)
    {
        await _mediaSessionService.SkipNextAsync();
    }

    private void MusicProgressTimer_Tick(object? sender, EventArgs e)
    {
        UpdateMusicProgressPresentation();
    }

    private void UpdateMusicProgressPresentation()
    {
        if (MusicProgressSlider is null || _isMusicProgressDragging)
        {
            return;
        }

        var duration = _currentNowPlayingState.Duration;
        if (duration <= TimeSpan.Zero)
        {
            MusicProgressSlider.Maximum = 1;
            MusicProgressSlider.Value = 0;
            MusicCurrentTimeText.Text = "0:00";
            MusicDurationText.Text = "0:00";
            return;
        }

        var position = _currentNowPlayingState.Position;

        if (_currentNowPlayingState.IsPlaying)
        {
            position += DateTime.UtcNow - _currentNowPlayingState.CapturedAtUtc;
        }

        if (position < TimeSpan.Zero)
        {
            position = TimeSpan.Zero;
        }

        if (position > duration)
        {
            position = duration;
        }

        MusicProgressSlider.Maximum = duration.TotalMilliseconds;
        MusicProgressSlider.Value = position.TotalMilliseconds;
        MusicCurrentTimeText.Text = FormatTime(position);
        MusicDurationText.Text = FormatTime(duration);
    }

    private static BitmapImage? CreateArtworkImage(byte[]? artworkBytes)
    {
        if (artworkBytes is null || artworkBytes.Length == 0)
        {
            return null;
        }

        try
        {
            using var memoryStream = new MemoryStream(artworkBytes);

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = memoryStream;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    private void MusicProgressSlider_DragStarted(object sender, DragStartedEventArgs e)
    {
        _isMusicProgressDragging = true;
    }

    private async void MusicProgressSlider_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        if (_currentNowPlayingState.Duration <= TimeSpan.Zero)
        {
            _isMusicProgressDragging = false;
            return;
        }

        var seekPosition = TimeSpan.FromMilliseconds(MusicProgressSlider.Value);
        _pendingSeekPosition = seekPosition;
        _pendingSeekUntilUtc = DateTime.UtcNow.AddSeconds(2);
        _currentNowPlayingState = new NowPlayingState
        {
            HasSession = _currentNowPlayingState.HasSession,
            Title = _currentNowPlayingState.Title,
            Artist = _currentNowPlayingState.Artist,
            IsPlaying = _currentNowPlayingState.IsPlaying,
            CanPlayPause = _currentNowPlayingState.CanPlayPause,
            CanGoNext = _currentNowPlayingState.CanGoNext,
            CanGoPrevious = _currentNowPlayingState.CanGoPrevious,
            Duration = _currentNowPlayingState.Duration,
            Position = seekPosition,
            CapturedAtUtc = DateTime.UtcNow,
            SourceAppId = _currentNowPlayingState.SourceAppId,
            ArtworkBytes = _currentNowPlayingState.ArtworkBytes,
        };

        _isMusicProgressDragging = false;
        UpdateMusicProgressPresentation();
        await _mediaSessionService.SeekAsync(seekPosition);
    }

    private NowPlayingState NormalizeNowPlayingState(NowPlayingState state)
    {
        if (_pendingSeekPosition is null || _isMusicProgressDragging)
        {
            return state;
        }

        if (DateTime.UtcNow >= _pendingSeekUntilUtc)
        {
            _pendingSeekPosition = null;
            return state;
        }

        var delta = (state.Position - _pendingSeekPosition.Value).Duration();
        if (delta <= TimeSpan.FromSeconds(2))
        {
            _pendingSeekPosition = null;
            return state;
        }

        return new NowPlayingState
        {
            HasSession = state.HasSession,
            Title = state.Title,
            Artist = state.Artist,
            IsPlaying = state.IsPlaying,
            CanPlayPause = state.CanPlayPause,
            CanGoNext = state.CanGoNext,
            CanGoPrevious = state.CanGoPrevious,
            Position = _pendingSeekPosition.Value,
            Duration = state.Duration,
            CapturedAtUtc = DateTime.UtcNow,
            SourceAppId = state.SourceAppId,
            ArtworkBytes = state.ArtworkBytes,
        };
    }

    private static string FormatTime(TimeSpan value)
    {
        var totalHours = (int)value.TotalHours;
        return totalHours > 0
            ? $"{totalHours}:{value.Minutes:00}:{value.Seconds:00}"
            : $"{value.Minutes}:{value.Seconds:00}";
    }
}
