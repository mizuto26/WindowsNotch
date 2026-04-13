using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using WindowsNotch.App.Models;
using WindowsNotch.App.Services;

namespace WindowsNotch.App;

public partial class MainWindow : Window
{
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
    private const int CollapseDelayMilliseconds = 180;
    private const int TopmostRefreshMilliseconds = 500;

    private readonly DispatcherTimer _hoverTimer;
    private readonly DispatcherTimer _topmostTimer;
    private readonly DispatcherTimer _collapseAnimationTimer;
    private readonly DispatcherTimer _dragPreviewTimer;
    private readonly ICloudDriveLocator _iCloudDriveLocator;
    private readonly ShelfService _shelfService;
    private readonly AppSettingsService _settingsService;
    private readonly StartupRegistrationService _startupRegistrationService;
    private readonly ObservableCollection<ShelfItem> _shelfItems = [];
    private readonly Dictionary<string, int> _animationVersions = [];

    private bool _isExpanded;
    private bool _isDragOver;
    private bool _isCollapseAnimationActive;
    private bool _isShareDropTargetActive;
    private bool _isShelfDropTargetActive;
    private bool _isOverlayModeActive;
    private bool _isShelfItemDragActive;
    private DateTime _lastInteractiveUtc = DateTime.UtcNow;
    private IntPtr _windowHandle = IntPtr.Zero;
    private double _expandedContentHeight;
    private double _expandedWindowHeight;
    private Point _shelfDragStartPoint;
    private ShelfItem? _selectedShelfItem;
    private AppSettings _settings;

    public MainWindow()
    {
        InitializeComponent();

        _iCloudDriveLocator = new ICloudDriveLocator();
        _shelfService = new ShelfService(_iCloudDriveLocator);
        _settingsService = new AppSettingsService();
        _startupRegistrationService = new StartupRegistrationService();
        _settings = _settingsService.Load();
        _settings.LaunchAtStartup = _startupRegistrationService.IsEnabled();

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
        _dragPreviewTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16),
        };

        _hoverTimer.Tick += HoverTimer_Tick;
        _topmostTimer.Tick += TopmostTimer_Tick;
        _collapseAnimationTimer.Tick += CollapseAnimationTimer_Tick;
        _dragPreviewTimer.Tick += DragPreviewTimer_Tick;
        SourceInitialized += Window_SourceInitialized;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        Width = ExpandedWidth;
        Height = CollapsedHeight;
        NotchScaleTransform.ScaleX = GetCollapsedScaleX();
        NotchScaleTransform.ScaleY = 1.0;

        ApplyWindowModeSettings();
        LoadShelfItems();
        UpdateDropZoneVisuals();
        PositionWindow();
        _hoverTimer.Start();
    }

    private void Window_SourceInitialized(object? sender, EventArgs e)
    {
        _windowHandle = new WindowInteropHelper(this).Handle;
        ApplyWindowModeSettings();
    }

    private void NotchBody_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateAnimatedNotchShape();
    }
}
