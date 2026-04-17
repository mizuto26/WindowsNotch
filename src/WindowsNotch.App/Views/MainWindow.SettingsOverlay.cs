using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media.Animation;
using WindowsNotch.App.Models;

namespace WindowsNotch.App;

public partial class MainWindow
{
    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        _lastInteractiveUtc = DateTime.UtcNow;

        var wasHoverTimerRunning = _hoverTimer.IsEnabled;
        _hoverTimer.Stop();
        RefreshOverlayMode(isInteractive: true);

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

    private void RefreshOverlayModeForCurrentState(bool immediateTopUpdate = false)
    {
        RefreshOverlayMode(isInteractive: false, immediateTopUpdate);
    }

    private void RefreshOverlayMode(bool isInteractive, bool immediateTopUpdate = false)
    {
        UpdateOverlayMode(ShouldDisplayOverlay(isInteractive), immediateTopUpdate);
    }

    private bool ShouldDisplayOverlay(bool isInteractive)
    {
        var isCovered = IsOtherWindowCoveringNotchArea();

        return isInteractive ||
               IsPresented ||
               _isCollapseAnimationActive ||
               !IsNotchCoveredLongEnough(isCovered);
    }

    private bool ShouldDisplayOverlayAfterCollapse()
    {
        return _isDragOver ||
               _isShareDropTargetActive ||
               _isShelfDropTargetActive ||
               !IsOtherWindowCoveringNotchArea();
    }

    private bool IsNotchCoveredLongEnough(bool isCovered)
    {
        if (!isCovered)
        {
            _notchCoveredSinceUtc = null;
            return false;
        }

        _notchCoveredSinceUtc ??= DateTime.UtcNow;
        return DateTime.UtcNow - _notchCoveredSinceUtc.Value >= TimeSpan.FromMilliseconds(OverlayHideDelayMilliseconds);
    }

    private bool IsOtherWindowCoveringNotchArea()
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

        if (!TryGetVisibleWindowRect(foregroundWindowHandle, out var windowRect))
        {
            return false;
        }

        var monitorHandle = MonitorFromWindow(_windowHandle, MONITOR_DEFAULTTONEAREST);
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

        var notchLeft = GetWindowLeft(CollapsedWidth) + WindowHorizontalMargin;
        var notchTop = monitorInfo.Monitor.Top;
        var notchRight = GetWindowLeft(CollapsedWidth) + CollapsedWidth - WindowHorizontalMargin;
        var notchBottom = monitorInfo.Monitor.Top + CollapsedHeight;
        const int overlapThreshold = 6;

        var horizontalOverlap = Math.Min(windowRect.Right, (int)Math.Round(notchRight)) -
                                Math.Max(windowRect.Left, (int)Math.Round(notchLeft));
        var verticalOverlap = Math.Min(windowRect.Bottom, (int)Math.Round(notchBottom)) -
                              Math.Max(windowRect.Top, notchTop);

        return horizontalOverlap >= overlapThreshold &&
               verticalOverlap >= overlapThreshold;
    }

    private void UpdateOverlayMode(bool overlayModeActive, bool immediateTopUpdate = false)
    {
        if (_isOverlayModeActive == overlayModeActive)
        {
            if (immediateTopUpdate)
            {
                ApplyWindowTop(GetWindowTop(overlayModeActive));
            }

            return;
        }

        _isOverlayModeActive = overlayModeActive;
        Topmost = overlayModeActive;

        if (!overlayModeActive && !immediateTopUpdate)
        {
            AnimateWindowDimension(TopProperty, GetWindowTop(overlayModeActive: false), CollapseAnimationMilliseconds, new CubicEase
            {
                EasingMode = EasingMode.EaseOut,
            });
        }
        else
        {
            ApplyWindowTop(GetWindowTop(overlayModeActive));
        }

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

    private void ApplyWindowBounds(double left, double top, double width, double height)
    {
        BeginAnimation(TopProperty, null);
        BeginAnimation(LeftProperty, null);
        BeginAnimation(WidthProperty, null);
        BeginAnimation(HeightProperty, null);

        if (_windowHandle != IntPtr.Zero)
        {
            SetWindowPos(
                _windowHandle,
                IntPtr.Zero,
                (int)Math.Round(left),
                (int)Math.Round(top),
                (int)Math.Round(width),
                (int)Math.Round(height),
                SWP_NOACTIVATE | SWP_SHOWWINDOW);
        }

        SetCurrentValue(LeftProperty, left);
        SetCurrentValue(TopProperty, top);
        SetCurrentValue(WidthProperty, width);
        SetCurrentValue(HeightProperty, height);
    }

    private void ApplyWindowTop(double top)
    {
        BeginAnimation(TopProperty, null);

        if (_windowHandle != IntPtr.Zero)
        {
            SetWindowPos(
                _windowHandle,
                IntPtr.Zero,
                (int)Math.Round(Left),
                (int)Math.Round(top),
                0,
                0,
                SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
        }

        SetCurrentValue(TopProperty, top);
    }
}
