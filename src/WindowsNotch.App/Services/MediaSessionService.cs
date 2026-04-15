using System.IO;
using Windows.Media.Control;
using Windows.Storage.Streams;
using WindowsNotch.App.Models;

namespace WindowsNotch.App.Services;

public sealed class MediaSessionService : IDisposable
{
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _currentSession;

    public event EventHandler<NowPlayingState>? StateChanged;

    public async Task InitializeAsync()
    {
        _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        _manager.CurrentSessionChanged += Manager_CurrentSessionChanged;
        _manager.SessionsChanged += Manager_SessionsChanged;

        AttachToSession(_manager.GetCurrentSession() ?? _manager.GetSessions().FirstOrDefault());
        await PublishStateAsync();
    }

    public async Task TogglePlayPauseAsync()
    {
        if (_currentSession is null)
        {
            return;
        }

        await _currentSession.TryTogglePlayPauseAsync();
        await PublishStateAsync();
    }

    public async Task SkipNextAsync()
    {
        if (_currentSession is null)
        {
            return;
        }

        await _currentSession.TrySkipNextAsync();
        await PublishStateAsync();
    }

    public async Task SkipPreviousAsync()
    {
        if (_currentSession is null)
        {
            return;
        }

        await _currentSession.TrySkipPreviousAsync();
        await PublishStateAsync();
    }

    public async Task SeekAsync(TimeSpan position)
    {
        if (_currentSession is null)
        {
            return;
        }

        if (position < TimeSpan.Zero)
        {
            position = TimeSpan.Zero;
        }

        await _currentSession.TryChangePlaybackPositionAsync(position.Ticks);
    }

    private async void Manager_CurrentSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args)
    {
        AttachToSession(sender.GetCurrentSession() ?? sender.GetSessions().FirstOrDefault());
        await PublishStateAsync();
    }

    private async void Manager_SessionsChanged(GlobalSystemMediaTransportControlsSessionManager sender, SessionsChangedEventArgs args)
    {
        if (_currentSession is null || !sender.GetSessions().Contains(_currentSession))
        {
            AttachToSession(sender.GetCurrentSession() ?? sender.GetSessions().FirstOrDefault());
        }

        await PublishStateAsync();
    }

    private async void CurrentSession_MediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args)
    {
        await PublishStateAsync();
    }

    private async void CurrentSession_PlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args)
    {
        await PublishStateAsync();
    }

    private async void CurrentSession_TimelinePropertiesChanged(GlobalSystemMediaTransportControlsSession sender, TimelinePropertiesChangedEventArgs args)
    {
        await PublishStateAsync();
    }

    private void AttachToSession(GlobalSystemMediaTransportControlsSession? session)
    {
        if (_currentSession is not null)
        {
            _currentSession.MediaPropertiesChanged -= CurrentSession_MediaPropertiesChanged;
            _currentSession.PlaybackInfoChanged -= CurrentSession_PlaybackInfoChanged;
            _currentSession.TimelinePropertiesChanged -= CurrentSession_TimelinePropertiesChanged;
        }

        _currentSession = session;

        if (_currentSession is not null)
        {
            _currentSession.MediaPropertiesChanged += CurrentSession_MediaPropertiesChanged;
            _currentSession.PlaybackInfoChanged += CurrentSession_PlaybackInfoChanged;
            _currentSession.TimelinePropertiesChanged += CurrentSession_TimelinePropertiesChanged;
        }
    }

    private async Task PublishStateAsync()
    {
        var state = await BuildStateAsync();
        StateChanged?.Invoke(this, state);
    }

    private async Task<NowPlayingState> BuildStateAsync()
    {
        if (_currentSession is null)
        {
            return new NowPlayingState();
        }

        GlobalSystemMediaTransportControlsSessionMediaProperties? mediaProperties = null;

        try
        {
            mediaProperties = await _currentSession.TryGetMediaPropertiesAsync();
        }
        catch
        {
            mediaProperties = null;
        }

        var playbackInfo = _currentSession.GetPlaybackInfo();
        var timeline = _currentSession.GetTimelineProperties();
        var controls = playbackInfo?.Controls;
        var title = mediaProperties?.Title;
        var artist = mediaProperties?.Artist;
        var duration = timeline.EndTime > TimeSpan.Zero ? timeline.EndTime : TimeSpan.Zero;
        var position = timeline.Position < TimeSpan.Zero ? TimeSpan.Zero : timeline.Position;

        return new NowPlayingState
        {
            HasSession = true,
            Title = string.IsNullOrWhiteSpace(title) ? "Unknown title" : title,
            Artist = string.IsNullOrWhiteSpace(artist) ? "Unknown artist" : artist,
            IsPlaying = playbackInfo?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing,
            CanPlayPause = controls?.IsPlayPauseToggleEnabled == true,
            CanGoNext = controls?.IsNextEnabled == true,
            CanGoPrevious = controls?.IsPreviousEnabled == true,
            Position = position,
            Duration = duration,
            CapturedAtUtc = DateTime.UtcNow,
            SourceAppId = _currentSession.SourceAppUserModelId ?? string.Empty,
            ArtworkBytes = await LoadArtworkBytesAsync(mediaProperties?.Thumbnail),
        };
    }

    private static async Task<byte[]?> LoadArtworkBytesAsync(IRandomAccessStreamReference? thumbnailReference)
    {
        if (thumbnailReference is null)
        {
            return null;
        }

        try
        {
            using var stream = await thumbnailReference.OpenReadAsync();
            using var managedStream = stream.AsStreamForRead();
            using var memoryStream = new MemoryStream();

            await managedStream.CopyToAsync(memoryStream);
            return memoryStream.ToArray();
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        if (_currentSession is not null)
        {
            _currentSession.MediaPropertiesChanged -= CurrentSession_MediaPropertiesChanged;
            _currentSession.PlaybackInfoChanged -= CurrentSession_PlaybackInfoChanged;
            _currentSession.TimelinePropertiesChanged -= CurrentSession_TimelinePropertiesChanged;
        }

        if (_manager is not null)
        {
            _manager.CurrentSessionChanged -= Manager_CurrentSessionChanged;
            _manager.SessionsChanged -= Manager_SessionsChanged;
        }
    }
}
