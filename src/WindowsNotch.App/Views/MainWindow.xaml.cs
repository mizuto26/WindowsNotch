using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using WindowsNotch.App.Models;
using WindowsNotch.App.Services;

namespace WindowsNotch.App;

public partial class MainWindow : Window
{
    private enum NotchExpansionStage
    {
        Collapsed,
        Preview,
        Expanded,
    }

    private enum ExpandedMode
    {
        Files,
        Music,
    }

    private const double WindowHorizontalMargin = 6;
    private const double WindowBottomMargin = 10;
    private const double VisualBottomCornerRadius = 32;
    private const double CollapsedWidth = 220;
    private const double ExpandedWidth = 620;
    private const double CollapsedHeight = 52;
    private const double ExpandedContentTopSpacing = 6;
    private const double VisibleWindowTopOffset = 0;
    private const double HiddenRevealHeight = 0;
    private const double HotZoneHeight = 6;
    private const double HotZoneHalfWidth = 188;
    private const int ExpandAnimationMilliseconds = 280;
    private const int CollapseAnimationMilliseconds = 240;
    private const int HoverPollMilliseconds = 16;
    private const int PreviewExpandDelayMilliseconds = 220;
    private const int CollapseDelayMilliseconds = 180;
    private const int PostDropKeepOpenMilliseconds = 1400;
    private const int PostDropExitDelayMilliseconds = 260;
    private const int OverlayHideDelayMilliseconds = 140;
    private const int TopmostRefreshMilliseconds = 500;
    private const int ModeSwitchDebounceMilliseconds = 220;
    private const double PreviewScaleProgress = 0.12;
    private const double PreviewHeightMultiplier = 1.08;

    private readonly DispatcherTimer _hoverTimer;
    private readonly DispatcherTimer _topmostTimer;
    private readonly DispatcherTimer _collapseAnimationTimer;
    private readonly DispatcherTimer _shareStatusResetTimer;
    private readonly ICloudDriveLocator _iCloudDriveLocator;
    private readonly ShelfService _shelfService;
    private readonly AppSettingsService _settingsService;
    private readonly StartupRegistrationService _startupRegistrationService;
    private readonly MediaSessionService _mediaSessionService;
    private readonly ObservableCollection<ShelfItem> _shelfItems = [];
    private readonly Dictionary<string, int> _animationVersions = [];

    private NotchExpansionStage _expansionStage = NotchExpansionStage.Collapsed;
    private bool _isDragOver;
    private bool _isCollapseAnimationActive;
    private bool _isShareDropTargetActive;
    private bool _isShelfDropTargetActive;
    private bool _isOverlayModeActive;
    private bool _isWaitingForPostDropExit;
    private DateTime? _postDropExitStartedUtc;
    private DateTime? _notchCoveredSinceUtc;
    private DateTime? _hoverStartedUtc;
    private DateTime? _keepExpandedUntilUtc;
    private bool? _pendingCollapseOverlayModeActive;
    private DateTime _lastInteractiveUtc = DateTime.UtcNow;
    private IntPtr _windowHandle = IntPtr.Zero;
    private double _expandedContentHeight;
    private double _expandedWindowHeight;
    private Point _shelfDragStartPoint;
    private ShelfItem? _selectedShelfItem;
    private AppSettings _settings;
    private ExpandedMode _expandedMode = ExpandedMode.Files;
    private DateTime _lastModeSwitchUtc = DateTime.MinValue;

    private bool IsExpanded => _expansionStage == NotchExpansionStage.Expanded;
    private bool IsPresented => _expansionStage != NotchExpansionStage.Collapsed;

    public MainWindow()
    {
        InitializeComponent();

        _iCloudDriveLocator = new ICloudDriveLocator();
        _shelfService = new ShelfService(_iCloudDriveLocator);
        _settingsService = new AppSettingsService();
        _startupRegistrationService = new StartupRegistrationService();
        _mediaSessionService = new MediaSessionService();
        _settings = _settingsService.Load();
        _settings.LaunchAtStartup = _startupRegistrationService.IsEnabled();
        _mediaSessionService.StateChanged += MediaSessionService_StateChanged;
        InitializeMediaUi();

        ShelfList.ItemsSource = _shelfItems;

        _hoverTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(HoverPollMilliseconds),
        };
        _topmostTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(TopmostRefreshMilliseconds),
        };
        _collapseAnimationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(CollapseAnimationMilliseconds + 20),
        };
        _shareStatusResetTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(1500),
        };

        _hoverTimer.Tick += HoverTimer_Tick;
        _topmostTimer.Tick += TopmostTimer_Tick;
        _collapseAnimationTimer.Tick += CollapseAnimationTimer_Tick;
        _shareStatusResetTimer.Tick += ShareStatusResetTimer_Tick;
        SourceInitialized += Window_SourceInitialized;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        Width = ExpandedWidth;
        Height = CollapsedHeight;
        ApplyImmediateNotchScale(GetCollapsedScaleX(), 1.0);
        UpdateExpandedModePresentation();

        LoadShelfItems();
        UpdateDropZoneVisuals();
        PositionWindow();
        SetShareStatusIdle();
        _hoverTimer.Start();
        await InitializeMediaSessionAsync();
    }

    private void Window_SourceInitialized(object? sender, EventArgs e)
    {
        _windowHandle = new WindowInteropHelper(this).Handle;
        RefreshOverlayModeForCurrentState();
    }

    private void NotchBody_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateAnimatedNotchShape();
    }

    private void FilesModeButton_Click(object sender, RoutedEventArgs e)
    {
        SwitchExpandedMode(ExpandedMode.Files);
    }

    private void MusicModeButton_Click(object sender, RoutedEventArgs e)
    {
        SwitchExpandedMode(ExpandedMode.Music);
    }

    private void SwitchExpandedMode(ExpandedMode mode)
    {
        var now = DateTime.UtcNow;
        if (_expandedMode == mode)
        {
            return;
        }

        if (now - _lastModeSwitchUtc < TimeSpan.FromMilliseconds(ModeSwitchDebounceMilliseconds))
        {
            return;
        }

        _lastModeSwitchUtc = now;
        _expandedMode = mode;
        UpdateExpandedModePresentation();
        RecalculateExpandedLayout();
    }

    private void KeepExpandedAfterDrop()
    {
        _hoverStartedUtc = null;
        _isWaitingForPostDropExit = true;
        _postDropExitStartedUtc = null;
        _keepExpandedUntilUtc = DateTime.UtcNow.AddMilliseconds(PostDropKeepOpenMilliseconds);
        _lastInteractiveUtc = DateTime.UtcNow;
        _isCollapseAnimationActive = false;
        _pendingCollapseOverlayModeActive = null;
        _collapseAnimationTimer.Stop();
        RefreshOverlayMode(isInteractive: true);
        SetExpansionStage(NotchExpansionStage.Expanded);
    }

    private void SetShareStatusIdle()
    {
        _shareStatusResetTimer.Stop();
        ShareStatusBadge.Background = new SolidColorBrush(Color.FromArgb(18, 255, 255, 255));
        ShareCloudIcon.Visibility = Visibility.Visible;
        ShareSuccessIcon.Visibility = Visibility.Collapsed;
        ShareStatusText.Text = "iCloud Drive";
        ShareStatusText.Foreground = (Brush)FindResource("SubtleBrush");
    }

    private void SetShareStatusSuccess()
    {
        ShareStatusBadge.Background = new SolidColorBrush(Color.FromArgb(255, 170, 235, 120));
        ShareCloudIcon.Visibility = Visibility.Collapsed;
        ShareSuccessIcon.Visibility = Visibility.Visible;
        ShareStatusText.Text = "Added";
        ShareStatusText.Foreground = new SolidColorBrush(Color.FromArgb(255, 185, 255, 150));
        _shareStatusResetTimer.Stop();
        _shareStatusResetTimer.Start();
    }

    private void ShareStatusResetTimer_Tick(object? sender, EventArgs e)
    {
        _shareStatusResetTimer.Stop();
        SetShareStatusIdle();
    }

    private void UpdateExpandedModePresentation()
    {
        var isFilesMode = _expandedMode == ExpandedMode.Files;

        if (FilesView is not null)
        {
            FilesView.Visibility = isFilesMode ? Visibility.Visible : Visibility.Collapsed;
        }

        if (MusicView is not null)
        {
            MusicView.Visibility = isFilesMode ? Visibility.Collapsed : Visibility.Visible;
        }

        if (ModeSwitchPanel is not null)
        {
            ModeSwitchPanel.Visibility = IsExpanded ? Visibility.Visible : Visibility.Collapsed;
        }

        if (FilesModeButton is not null)
        {
            FilesModeButton.Opacity = isFilesMode ? 1.0 : 0.52;
            FilesModeButton.IsHitTestVisible = !isFilesMode;
        }

        if (MusicModeButton is not null)
        {
            MusicModeButton.Opacity = isFilesMode ? 0.52 : 1.0;
            MusicModeButton.IsHitTestVisible = isFilesMode;
        }
    }
}
