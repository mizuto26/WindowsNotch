using System.IO;
using System.Threading;
using Windows.Media.Control;
using Windows.Storage.Streams;
using WindowsNotch.App.Models;

namespace WindowsNotch.App.Services;

public sealed class MediaSessionService : IDisposable
{
    private readonly SemaphoreSlim _publishStateLock = new(1, 1);
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _currentSession;
    private int _publishStateVersion;
    private bool _isDisposed;

    public event EventHandler<NowPlayingState>? StateChanged;

    public async Task InitializeAsync()
    {
        ThrowIfDisposed();

        if (_manager is not null)
        {
            return;
        }

        _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        _manager.CurrentSessionChanged += Manager_CurrentSessionChanged;
        _manager.SessionsChanged += Manager_SessionsChanged;

        AttachToSession(GetCurrentOrFirstSession(_manager));
        await PublishStateAsync();
    }

    public async Task TogglePlayPauseAsync()
    {
        ThrowIfDisposed();

        if (_currentSession is null)
        {
            return;
        }

        await _currentSession.TryTogglePlayPauseAsync();
        await PublishStateAsync();
    }

    public async Task SkipNextAsync()
    {
        ThrowIfDisposed();

        if (_currentSession is null)
        {
            return;
        }

        await _currentSession.TrySkipNextAsync();
        await PublishStateAsync();
    }

    public async Task SkipPreviousAsync()
    {
        ThrowIfDisposed();

        if (_currentSession is null)
        {
            return;
        }

        await _currentSession.TrySkipPreviousAsync();
        await PublishStateAsync();
    }

    public async Task SeekAsync(TimeSpan position)
    {
        ThrowIfDisposed();

        if (_currentSession is null)
        {
            return;
        }

        if (position < TimeSpan.Zero)
        {
            position = TimeSpan.Zero;
        }

        await _currentSession.TryChangePlaybackPositionAsync(position.Ticks);
        await PublishStateAsync();
    }

    private async void Manager_CurrentSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args)
    {
        AttachToSession(GetCurrentOrFirstSession(sender));
        await PublishStateAsync();
    }

    private async void Manager_SessionsChanged(GlobalSystemMediaTransportControlsSessionManager sender, SessionsChangedEventArgs args)
    {
        if (_currentSession is null || !sender.GetSessions().Contains(_currentSession))
        {
            AttachToSession(GetCurrentOrFirstSession(sender));
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
        ThrowIfDisposed();

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
        if (_isDisposed)
        {
            return;
        }

        var publishVersion = Interlocked.Increment(ref _publishStateVersion);
        var lockTaken = false;

        try
        {
            await _publishStateLock.WaitAsync();
            lockTaken = true;

            if (_isDisposed)
            {
                return;
            }

            var state = await BuildStateAsync();
            if (_isDisposed || publishVersion != Volatile.Read(ref _publishStateVersion))
            {
                return;
            }

            StateChanged?.Invoke(this, state);
        }
        catch
        {
            if (_isDisposed)
            {
                return;
            }

            StateChanged?.Invoke(this, new NowPlayingState());
        }
        finally
        {
            if (lockTaken)
            {
                _publishStateLock.Release();
            }
        }
    }

    private async Task<NowPlayingState> BuildStateAsync()
    {
        var currentSession = _currentSession;
        if (currentSession is null)
        {
            return new NowPlayingState();
        }

        GlobalSystemMediaTransportControlsSessionMediaProperties? mediaProperties;

        try
        {
            mediaProperties = await currentSession.TryGetMediaPropertiesAsync();
        }
        catch
        {
            mediaProperties = null;
        }

        var playbackInfo = currentSession.GetPlaybackInfo();
        var timeline = currentSession.GetTimelineProperties();
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
            SourceAppId = currentSession.SourceAppUserModelId ?? string.Empty,
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
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

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

        _currentSession = null;
        _manager = null;
    }

    private static GlobalSystemMediaTransportControlsSession? GetCurrentOrFirstSession(GlobalSystemMediaTransportControlsSessionManager manager)
    {
        var currentSession = manager.GetCurrentSession();
        if (currentSession is not null)
        {
            return currentSession;
        }

        var sessions = manager.GetSessions();
        return sessions.Count > 0 ? sessions[0] : null;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
    }
}
