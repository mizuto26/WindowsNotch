using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using WindowsNotch.App.Models;
using WindowsNotch.App.Services;

namespace WindowsNotch.App;

public partial class MainWindow : Window
{
    private const double WindowHorizontalMargin = 6;
    private const double WindowBottomMargin = 10;
    private const double CollapsedWidth = 220;
    private const double ExpandedWidth = 620;
    private const double CollapsedHeight = 52;
    private const double ExpandedContentTopSpacing = 6;
    private const double VisibleWindowTopOffset = 0;
    private const double HiddenRevealHeight = 4;
    private const double HotZoneHeight = 6;
    private const double HotZoneHalfWidth = 188;
    private const int AnimationMilliseconds = 220;
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

    private bool _isExpanded;
    private bool _isDragOver;
    private bool _isCollapseAnimationActive;
    private bool _isShareDropTargetActive;
    private bool _isShelfDropTargetActive;
    private DateTime _lastInteractiveUtc = DateTime.UtcNow;
    private IntPtr _windowHandle = IntPtr.Zero;
    private double _expandedContentHeight;
    private double _expandedWindowHeight;
    private Point _shelfDragStartPoint;
    private ShelfItem? _selectedShelfItem;
    private bool _isOverlayModeActive;
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
            Interval = TimeSpan.FromMilliseconds(AnimationMilliseconds + 20),
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
        Width = CollapsedWidth;
        Height = CollapsedHeight;

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

    private void HoverTimer_Tick(object? sender, EventArgs e)
    {
        var cursorPoint = GetCursorPositionInDeviceIndependentPixels();
        var isInteractive =
            _isDragOver ||
            _isShareDropTargetActive ||
            _isShelfDropTargetActive ||
            IsCursorInHotZone(cursorPoint) ||
            (!_isCollapseAnimationActive && IsCursorOverNotchBody(cursorPoint));

        UpdateOverlayMode(ShouldDisplayOverlay(isInteractive));

        if (isInteractive)
        {
            _lastInteractiveUtc = DateTime.UtcNow;
            SetExpanded(true);
            return;
        }

        if (!_isExpanded)
        {
            return;
        }

        if (DateTime.UtcNow - _lastInteractiveUtc < TimeSpan.FromMilliseconds(CollapseDelayMilliseconds))
        {
            return;
        }

        SetExpanded(false);
    }

    private void TopmostTimer_Tick(object? sender, EventArgs e)
    {
        if (!_isOverlayModeActive)
        {
            _topmostTimer.Stop();
            return;
        }

        UpdateWindowZOrder(HWND_TOPMOST);
    }

    private void CollapseAnimationTimer_Tick(object? sender, EventArgs e)
    {
        _collapseAnimationTimer.Stop();
        _isCollapseAnimationActive = false;
        SettingsButton.Visibility = _isExpanded ? Visibility.Visible : Visibility.Collapsed;
        UpdateOverlayMode(ShouldDisplayOverlay(isInteractive: false));
    }

    private bool IsCursorInHotZone(Point cursorPoint)
    {
        var centerX = SystemParameters.PrimaryScreenWidth / 2.0;
        return cursorPoint.Y <= HotZoneHeight && Math.Abs(cursorPoint.X - centerX) <= HotZoneHalfWidth;
    }

    private bool IsCursorOverNotchBody(Point cursorPoint)
    {
        var hoverWidth = (_isExpanded || _isCollapseAnimationActive) ? ExpandedWidth : CollapsedWidth;
        var hoverHeight = (_isExpanded || _isCollapseAnimationActive) ? _expandedWindowHeight : CollapsedHeight;
        var hoverLeft = GetWindowLeft(hoverWidth);
        var bodyLeft = hoverLeft + WindowHorizontalMargin;
        var bodyTop = Top;
        var bodyRight = hoverLeft + hoverWidth - WindowHorizontalMargin;
        var bodyBottom = Top + hoverHeight - WindowBottomMargin;

        return cursorPoint.X >= bodyLeft &&
               cursorPoint.X <= bodyRight &&
               cursorPoint.Y >= bodyTop &&
               cursorPoint.Y <= bodyBottom;
    }

    private void SetExpanded(bool expanded)
    {
        if (_isExpanded == expanded)
        {
            return;
        }

        _isExpanded = expanded;
        _isCollapseAnimationActive = !expanded;

        if (expanded)
        {
            SettingsButton.Visibility = Visibility.Visible;
        }

        var targetWidth = expanded ? ExpandedWidth : CollapsedWidth;
        var targetHeight = expanded ? _expandedWindowHeight : CollapsedHeight;
        var targetContentHeight = expanded ? _expandedContentHeight : 0.0;

        if (expanded)
        {
            _collapseAnimationTimer.Stop();
        }
        else
        {
            _collapseAnimationTimer.Stop();
            _collapseAnimationTimer.Start();
        }

        UpdateOverlayMode(ShouldDisplayOverlay(expanded || _isDragOver || _isShareDropTargetActive || _isShelfDropTargetActive));

        AnimateWindowDimension(WidthProperty, targetWidth);
        AnimateWindowDimension(HeightProperty, targetHeight);
        AnimateWindowDimension(LeftProperty, GetWindowLeft(targetWidth));
        AnimateElementDimension(ExpandedContentViewport, FrameworkElement.HeightProperty, targetContentHeight);
    }

    private void AnimateWindowDimension(DependencyProperty property, double targetValue)
    {
        var animation = new DoubleAnimation
        {
            From = (double)GetValue(property),
            To = targetValue,
            Duration = TimeSpan.FromMilliseconds(AnimationMilliseconds),
            FillBehavior = FillBehavior.Stop,
            EasingFunction = new CubicEase
            {
                EasingMode = EasingMode.EaseInOut,
            },
        };

        animation.Completed += (_, _) =>
        {
            BeginAnimation(property, null);
            SetCurrentValue(property, targetValue);
        };

        BeginAnimation(property, animation, HandoffBehavior.SnapshotAndReplace);
    }

    private static void AnimateElementDimension(FrameworkElement element, DependencyProperty property, double targetValue)
    {
        var animation = new DoubleAnimation
        {
            From = (double)element.GetValue(property),
            To = targetValue,
            Duration = TimeSpan.FromMilliseconds(AnimationMilliseconds),
            FillBehavior = FillBehavior.Stop,
            EasingFunction = new CubicEase
            {
                EasingMode = EasingMode.EaseInOut,
            },
        };

        animation.Completed += (_, _) =>
        {
            element.BeginAnimation(property, null);
            element.SetCurrentValue(property, targetValue);
        };

        element.BeginAnimation(property, animation, HandoffBehavior.SnapshotAndReplace);
    }

    private void PositionWindow()
    {
        Left = GetWindowLeft(Width);
        Top = GetWindowTop(overlayModeActive: ShouldDisplayOverlay(isInteractive: false));
        SettingsButton.Visibility = Visibility.Collapsed;
        UpdateOverlayMode(ShouldDisplayOverlay(isInteractive: false));
    }

    private static double GetWindowLeft(double width)
    {
        return (SystemParameters.PrimaryScreenWidth - width) / 2.0;
    }

    private static double GetWindowTop(bool overlayModeActive)
    {
        return overlayModeActive ? VisibleWindowTopOffset : -(CollapsedHeight - HiddenRevealHeight);
    }

    private void Window_DragEnter(object sender, DragEventArgs e)
    {
        UpdateDragState(e);
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        UpdateDragState(e);
    }

    private void Window_DragLeave(object sender, DragEventArgs e)
    {
        _isDragOver = false;
        _isShareDropTargetActive = false;
        _isShelfDropTargetActive = false;
        UpdateDropZoneVisuals();
        UpdateOverlayMode(ShouldDisplayOverlay(isInteractive: false));
        _lastInteractiveUtc = DateTime.UtcNow;
        e.Handled = true;
    }

    private void UpdateDragState(DragEventArgs e)
    {
        var hasFileDrop = e.Data.GetDataPresent(DataFormats.FileDrop);
        _isDragOver = hasFileDrop;
        e.Effects = hasFileDrop ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;

        if (hasFileDrop)
        {
            _lastInteractiveUtc = DateTime.UtcNow;
            UpdateOverlayMode(ShouldDisplayOverlay(isInteractive: true));
            SetExpanded(true);
        }
    }

    private void SharePanel_DragEnter(object sender, DragEventArgs e)
    {
        UpdateZoneDragState(e, shareZone: true);
    }

    private void SharePanel_DragOver(object sender, DragEventArgs e)
    {
        UpdateZoneDragState(e, shareZone: true);
    }

    private void SharePanel_DragLeave(object sender, DragEventArgs e)
    {
        _isShareDropTargetActive = false;
        UpdateDropZoneVisuals();
        UpdateOverlayMode(ShouldDisplayOverlay(isInteractive: false));
        e.Handled = true;
    }

    private async void SharePanel_Drop(object sender, DragEventArgs e)
    {
        _isShareDropTargetActive = false;
        _isDragOver = false;
        _lastInteractiveUtc = DateTime.UtcNow;
        UpdateDropZoneVisuals();
        UpdateOverlayMode(ShouldDisplayOverlay(isInteractive: true));
        e.Handled = true;

        if (e.Data.GetData(DataFormats.FileDrop) is not string[] entries || entries.Length == 0)
        {
            return;
        }

        try
        {
            var result = await _shelfService.SendEntriesToICloudAsync(entries);
            if (!result.Success)
            {
                MessageBox.Show(this, result.Message, "WindowsNotch", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "WindowsNotch", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ShelfPanel_DragEnter(object sender, DragEventArgs e)
    {
        UpdateZoneDragState(e, shareZone: false);
    }

    private void ShelfPanel_DragOver(object sender, DragEventArgs e)
    {
        UpdateZoneDragState(e, shareZone: false);
    }

    private void ShelfPanel_DragLeave(object sender, DragEventArgs e)
    {
        _isShelfDropTargetActive = false;
        UpdateDropZoneVisuals();
        UpdateOverlayMode(ShouldDisplayOverlay(isInteractive: false));
        e.Handled = true;
    }

    private async void ShelfPanel_Drop(object sender, DragEventArgs e)
    {
        _isShelfDropTargetActive = false;
        _isDragOver = false;
        _lastInteractiveUtc = DateTime.UtcNow;
        UpdateDropZoneVisuals();
        UpdateOverlayMode(ShouldDisplayOverlay(isInteractive: true));
        e.Handled = true;

        if (e.Data.GetData(DataFormats.FileDrop) is not string[] entries || entries.Length == 0)
        {
            return;
        }

        try
        {
            var updatedItems = await _shelfService.StashEntriesAsync(entries);
            ReplaceShelfItems(updatedItems);
            SetExpanded(true);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "WindowsNotch", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void UpdateZoneDragState(DragEventArgs e, bool shareZone)
    {
        var hasFileDrop = e.Data.GetDataPresent(DataFormats.FileDrop);
        _isDragOver = hasFileDrop;
        _isShareDropTargetActive = hasFileDrop && shareZone;
        _isShelfDropTargetActive = hasFileDrop && !shareZone;
        e.Effects = hasFileDrop ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;

        if (hasFileDrop)
        {
            _lastInteractiveUtc = DateTime.UtcNow;
            UpdateOverlayMode(ShouldDisplayOverlay(isInteractive: true));
            SetExpanded(true);
        }

        UpdateDropZoneVisuals();
    }

    private void ShelfList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _shelfDragStartPoint = e.GetPosition(ShelfList);
    }

    private void ShelfList_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var currentPosition = e.GetPosition(ShelfList);
        if (Math.Abs(currentPosition.X - _shelfDragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(currentPosition.Y - _shelfDragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        if (!TryGetShelfItemFromSource(e.OriginalSource as DependencyObject, out var item))
        {
            return;
        }

        if (!File.Exists(item.StoredPath) && !Directory.Exists(item.StoredPath))
        {
            return;
        }

        _lastInteractiveUtc = DateTime.UtcNow;
        BeginShelfDrag(item);
    }

    private void BeginShelfDrag(ShelfItem item)
    {
        var data = new DataObject(DataFormats.FileDrop, new[] { item.StoredPath });
        ShowDragPreview(item);

        try
        {
            DragDrop.DoDragDrop(ShelfList, data, DragDropEffects.Copy);
        }
        finally
        {
            HideDragPreview();
        }
    }

    private void ShelfList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedShelfItem = ShelfList.SelectedItem as ShelfItem;
    }

    private bool TryGetShelfItemFromSource(DependencyObject? source, out ShelfItem item)
    {
        item = null!;

        if (source is null)
        {
            return false;
        }

        var container = ItemsControl.ContainerFromElement(ShelfList, source) as ListBoxItem;
        if (container?.Content is not ShelfItem shelfItem)
        {
            return false;
        }

        item = shelfItem;
        return true;
    }

    private void LoadShelfItems()
    {
        ReplaceShelfItems(_shelfService.LoadItems());
    }

    private void ReplaceShelfItems(IEnumerable<ShelfItem> items)
    {
        var selectedPath = _selectedShelfItem?.StoredPath;
        _shelfItems.Clear();

        foreach (var item in items)
        {
            _shelfItems.Add(item);
        }

        _selectedShelfItem = selectedPath is null
            ? _shelfItems.FirstOrDefault()
            : _shelfItems.FirstOrDefault(item => item.StoredPath == selectedPath) ?? _shelfItems.FirstOrDefault();

        ShelfList.SelectedItem = _selectedShelfItem;
        UpdateShelfPresentation();
    }

    private void UpdateShelfPresentation()
    {
        EmptyShelfPanel.Visibility = _shelfItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ShelfList.Visibility = _shelfItems.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        RecalculateExpandedLayout();
    }

    private void UpdateDropZoneVisuals()
    {
        ShareDropZone.Background = _isShareDropTargetActive
            ? new SolidColorBrush(Color.FromArgb(42, 102, 209, 255))
            : new SolidColorBrush(Color.FromArgb(14, 255, 255, 255));

        ShareDropZone.BorderBrush = _isShareDropTargetActive
            ? new SolidColorBrush(Color.FromArgb(90, 102, 209, 255))
            : new SolidColorBrush(Color.FromArgb(24, 255, 255, 255));

        ShelfPanel.BorderBrush = _isShelfDropTargetActive
            ? new SolidColorBrush(Color.FromArgb(90, 102, 209, 255))
            : new SolidColorBrush(Color.FromArgb(24, 255, 255, 255));
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        _lastInteractiveUtc = DateTime.UtcNow;

        var wasHoverTimerRunning = _hoverTimer.IsEnabled;
        _hoverTimer.Stop();
        UpdateOverlayMode(ShouldDisplayOverlay(isInteractive: true));

        try
        {
            var settingsWindow = new SettingsWindow(CreateSettingsSnapshot(), _iCloudDriveLocator)
            {
                Owner = this,
            };

            if (settingsWindow.ShowDialog() != true)
            {
                return;
            }

            ApplySettings(settingsWindow.ResultSettings);

            if (settingsWindow.ShouldExitAfterClose)
            {
                Application.Current.Shutdown();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "WindowsNotch", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            if (wasHoverTimerRunning)
            {
                _hoverTimer.Start();
            }

            _lastInteractiveUtc = DateTime.UtcNow;
        }
    }

    private void ShowDragPreview(ShelfItem item)
    {
        DragPreviewNameText.Text = item.DisplayName;
        DragPreviewKindText.Text = item.KindLabel;

        if (item.ThumbnailSource is not null)
        {
            DragPreviewImage.Source = item.ThumbnailSource;
            DragPreviewImage.Visibility = Visibility.Visible;
            DragPreviewFallbackBadge.Visibility = Visibility.Collapsed;
        }
        else
        {
            DragPreviewImage.Source = null;
            DragPreviewImage.Visibility = Visibility.Collapsed;
            DragPreviewFallbackBadge.Visibility = Visibility.Visible;
        }

        UpdateDragPreviewPosition();
        DragPreviewPopup.IsOpen = true;
        _dragPreviewTimer.Start();
    }

    private void HideDragPreview()
    {
        _dragPreviewTimer.Stop();
        DragPreviewPopup.IsOpen = false;
    }

    private void DragPreviewTimer_Tick(object? sender, EventArgs e)
    {
        UpdateDragPreviewPosition();
    }

    private void UpdateDragPreviewPosition()
    {
        var cursorPoint = GetCursorPositionInDeviceIndependentPixels();

        if (DragPreviewPopup.Child is FrameworkElement previewElement)
        {
            previewElement.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var previewSize = previewElement.DesiredSize;

            DragPreviewPopup.HorizontalOffset = cursorPoint.X - (previewSize.Width / 2.0);
            DragPreviewPopup.VerticalOffset = cursorPoint.Y - (previewSize.Height / 2.0);
            return;
        }

        DragPreviewPopup.HorizontalOffset = cursorPoint.X;
        DragPreviewPopup.VerticalOffset = cursorPoint.Y;
    }

    private AppSettings CreateSettingsSnapshot()
    {
        return new AppSettings
        {
            LaunchAtStartup = _settings.LaunchAtStartup,
        };
    }

    private void ApplySettings(AppSettings settings)
    {
        _startupRegistrationService.SetEnabled(settings.LaunchAtStartup);
        settings.LaunchAtStartup = _startupRegistrationService.IsEnabled();
        _settings = settings;
        _settingsService.Save(_settings);
    }

    private void ApplyWindowModeSettings()
    {
        UpdateOverlayMode(ShouldDisplayOverlay(isInteractive: false));
    }

    private bool ShouldDisplayOverlay(bool isInteractive)
    {
        return isInteractive ||
               _isExpanded ||
               _isCollapseAnimationActive ||
               !IsOtherWindowFullscreen();
    }

    private bool IsOtherWindowFullscreen()
    {
        if (_windowHandle == IntPtr.Zero)
        {
            return false;
        }

        var foregroundWindowHandle = GetForegroundWindow();
        if (foregroundWindowHandle == IntPtr.Zero || foregroundWindowHandle == _windowHandle)
        {
            return false;
        }

        if (!IsWindowVisible(foregroundWindowHandle) || IsIconic(foregroundWindowHandle))
        {
            return false;
        }

        if (!GetWindowRect(foregroundWindowHandle, out var windowRect))
        {
            return false;
        }

        var monitorHandle = MonitorFromWindow(foregroundWindowHandle, MONITOR_DEFAULTTONEAREST);
        if (monitorHandle == IntPtr.Zero)
        {
            return false;
        }

        var monitorInfo = new MonitorInfo
        {
            cbSize = Marshal.SizeOf<MonitorInfo>(),
        };

        if (!GetMonitorInfo(monitorHandle, ref monitorInfo))
        {
            return false;
        }

        const int tolerance = 2;

        return Math.Abs(windowRect.Left - monitorInfo.Monitor.Left) <= tolerance &&
               Math.Abs(windowRect.Top - monitorInfo.Monitor.Top) <= tolerance &&
               Math.Abs(windowRect.Right - monitorInfo.Monitor.Right) <= tolerance &&
               Math.Abs(windowRect.Bottom - monitorInfo.Monitor.Bottom) <= tolerance;
    }

    private void UpdateOverlayMode(bool overlayModeActive)
    {
        if (_isOverlayModeActive == overlayModeActive)
        {
            return;
        }

        _isOverlayModeActive = overlayModeActive;
        Topmost = overlayModeActive;
        AnimateWindowDimension(TopProperty, GetWindowTop(overlayModeActive));

        if (overlayModeActive)
        {
            if (!_topmostTimer.IsEnabled)
            {
                _topmostTimer.Start();
            }

            UpdateWindowZOrder(HWND_TOPMOST);
            return;
        }

        if (_topmostTimer.IsEnabled)
        {
            _topmostTimer.Stop();
        }

        UpdateWindowZOrder(HWND_NOTOPMOST);
    }

    private void UpdateWindowZOrder(IntPtr windowOrder)
    {
        if (_windowHandle == IntPtr.Zero)
        {
            return;
        }

        SetWindowPos(
            _windowHandle,
            windowOrder,
            0,
            0,
            0,
            0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
    }

    private void RecalculateExpandedLayout()
    {
        ExpandedContentViewport.Height = double.NaN;
        ExpandedContentViewport.Opacity = 1.0;

        ExpandedContentRoot.Measure(new Size(ExpandedWidth - 32, double.PositiveInfinity));
        _expandedContentHeight = ExpandedContentRoot.DesiredSize.Height;
        _expandedWindowHeight = CollapsedHeight + ExpandedContentTopSpacing + _expandedContentHeight;

        if (_isExpanded)
        {
            ExpandedContentViewport.Height = _expandedContentHeight;
            Height = _expandedWindowHeight;
        }
        else
        {
            ExpandedContentViewport.Height = 0;
            Height = CollapsedHeight;
        }
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (HandleShelfNavigationKey(e.Key))
        {
            e.Handled = true;
            return;
        }

        if (e.Key != Key.Back)
        {
            return;
        }

        if (DeleteSelectedShelfItem())
        {
            e.Handled = true;
        }
    }

    private void ShelfList_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (HandleShelfNavigationKey(e.Key))
        {
            e.Handled = true;
            return;
        }

        if (e.Key != Key.Back)
        {
            return;
        }

        if (DeleteSelectedShelfItem())
        {
            e.Handled = true;
        }
    }

    private bool DeleteSelectedShelfItem()
    {
        if (_selectedShelfItem is null)
        {
            return false;
        }

        ReplaceShelfItems(_shelfService.RemoveItem(_selectedShelfItem));
        return true;
    }

    private bool HandleShelfNavigationKey(Key key)
    {
        if (key is not Key.Left and not Key.Right)
        {
            return false;
        }

        if (_shelfItems.Count == 0)
        {
            return false;
        }

        var currentIndex = ShelfList.SelectedIndex;
        if (currentIndex < 0)
        {
            currentIndex = 0;
        }

        var nextIndex = key == Key.Left
            ? Math.Max(0, currentIndex - 1)
            : Math.Min(_shelfItems.Count - 1, currentIndex + 1);

        if (nextIndex == currentIndex && ShelfList.SelectedItem is not null)
        {
            return true;
        }

        ShelfList.SelectedIndex = nextIndex;

        if (ShelfList.SelectedItem is ShelfItem selectedItem)
        {
            ShelfList.ScrollIntoView(selectedItem);
        }

        return true;
    }

    private Point GetCursorPositionInDeviceIndependentPixels()
    {
        if (!GetCursorPos(out var point))
        {
            return new Point(0, 0);
        }

        var source = PresentationSource.FromVisual(this);
        var transform = source?.CompositionTarget?.TransformFromDevice;

        if (transform is null)
        {
            return new Point(point.X, point.Y);
        }

        return transform.Value.Transform(new Point(point.X, point.Y));
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hWnd, out NativeRect lpRect);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo lpmi);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int X,
        int Y,
        int cx,
        int cy,
        uint uFlags);

    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private static readonly IntPtr HWND_NOTOPMOST = new(-2);
    private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_SHOWWINDOW = 0x0040;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MonitorInfo
    {
        public int cbSize;
        public NativeRect Monitor;
        public NativeRect WorkArea;
        public uint Flags;
    }
}
