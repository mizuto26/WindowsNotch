using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace WindowsNotch.App;

public partial class MainWindow
{
    private void HoverTimer_Tick(object? sender, EventArgs e)
    {
        UpdateAnimatedNotchShape();

        var cursorPoint = GetCursorPositionInDeviceIndependentPixels();
        if (Mouse.LeftButton == MouseButtonState.Pressed &&
            IsCursorInHotZone(cursorPoint))
        {
            _lastInteractiveUtc = DateTime.UtcNow;
            UpdateOverlayMode(ShouldDisplayOverlay(isInteractive: true));
            SetExpanded(true);
        }

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

        ApplyNotchStateWithoutFade(() =>
        {
            ApplyWindowBounds(GetWindowLeft(ExpandedWidth), Top, ExpandedWidth, CollapsedHeight);
            ExpandedContentViewport.Height = 0.0;
            ExpandedContentViewport.Opacity = 0.0;
            ExpandedContentScaleTransform.ScaleX = 0.97;
            ExpandedContentScaleTransform.ScaleY = 0.9;
            ExpandedContentTranslateTransform.Y = -6.0;
            NotchScaleTransform.ScaleX = GetCollapsedScaleX();
            NotchScaleTransform.ScaleY = 1.0;
            UpdateAnimatedNotchShape();
        });

        UpdateOverlayMode(ShouldDisplayOverlay(isInteractive: false));
    }

    private bool IsCursorInHotZone(Point cursorPoint)
    {
        var centerX = SystemParameters.PrimaryScreenWidth / 2.0;
        return cursorPoint.Y <= HotZoneHeight && Math.Abs(cursorPoint.X - centerX) <= HotZoneHalfWidth;
    }

    private bool IsCursorOverNotchBody(Point cursorPoint)
    {
        var scaleX = Math.Max(0.001, NotchScaleTransform.ScaleX);
        var scaleY = Math.Max(0.001, NotchScaleTransform.ScaleY);
        var bodyWidth = NotchBody.ActualWidth * scaleX;
        var bodyHeight = NotchBody.ActualHeight * scaleY;
        var bodyLeft = Left + WindowHorizontalMargin + Math.Max(0.0, (NotchBody.ActualWidth - bodyWidth) / 2.0);
        var bodyTop = Top;
        var bodyRight = bodyLeft + bodyWidth;
        var bodyBottom = bodyTop + bodyHeight;

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
            SettingsButton.Visibility = Visibility.Collapsed;
            ExpandedContentViewport.Opacity = 0.0;
            _collapseAnimationTimer.Stop();
            ApplyExpandedWindowState(() =>
            {
                ApplyWindowBounds(Left, GetWindowTop(overlayModeActive: true), ExpandedWidth, _expandedWindowHeight);
                ExpandedContentViewport.Height = _expandedContentHeight;
                ExpandedContentScaleTransform.ScaleX = 0.97;
                ExpandedContentScaleTransform.ScaleY = 0.9;
                ExpandedContentTranslateTransform.Y = -6.0;
            });
        }
        else
        {
            SettingsButton.Visibility = Visibility.Collapsed;
            _collapseAnimationTimer.Stop();
            _collapseAnimationTimer.Start();
        }

        var animationDuration = expanded ? ExpandAnimationMilliseconds : CollapseAnimationMilliseconds;
        var targetNotchScaleX = expanded ? 1.0 : GetCollapsedScaleX();
        var targetNotchScaleY = expanded ? 1.0 : GetCollapsedScaleY();
        var targetOpacity = expanded ? 1.0 : 0.0;
        var targetScaleX = expanded ? 1.0 : 0.97;
        var targetScaleY = expanded ? 1.0 : 0.9;
        var targetTranslateY = expanded ? 0.0 : -6.0;

        UpdateOverlayMode(
            ShouldDisplayOverlay(expanded || _isDragOver || _isShareDropTargetActive || _isShelfDropTargetActive),
            immediateTopUpdate: expanded);

        if (!expanded)
        {
            CancelTrackedAnimation(ExpandedContentViewport, FrameworkElement.HeightProperty);
            CancelTrackedAnimation(ExpandedContentViewport, UIElement.OpacityProperty);
            CancelTrackedAnimation(ExpandedContentScaleTransform, ScaleTransform.ScaleXProperty);
            CancelTrackedAnimation(ExpandedContentScaleTransform, ScaleTransform.ScaleYProperty);
            CancelTrackedAnimation(ExpandedContentTranslateTransform, TranslateTransform.YProperty);

            ExpandedContentViewport.Height = 0.0;
            ExpandedContentViewport.Opacity = 0.0;
            ExpandedContentScaleTransform.ScaleX = targetScaleX;
            ExpandedContentScaleTransform.ScaleY = targetScaleY;
            ExpandedContentTranslateTransform.Y = targetTranslateY;
        }

        AnimateElementDimension(NotchScaleTransform, ScaleTransform.ScaleXProperty, targetNotchScaleX, animationDuration, new QuinticEase
        {
            EasingMode = EasingMode.EaseOut,
        });
        AnimateElementDimension(NotchScaleTransform, ScaleTransform.ScaleYProperty, targetNotchScaleY, animationDuration, new ExponentialEase
        {
            EasingMode = EasingMode.EaseOut,
            Exponent = 5,
        });

        if (expanded)
        {
            AnimateElementDimension(ExpandedContentViewport, UIElement.OpacityProperty, targetOpacity, animationDuration, new QuadraticEase
            {
                EasingMode = EasingMode.EaseOut,
            });
            AnimateElementDimension(ExpandedContentScaleTransform, ScaleTransform.ScaleXProperty, targetScaleX, animationDuration, new QuinticEase
            {
                EasingMode = EasingMode.EaseOut,
            });
            AnimateElementDimension(ExpandedContentScaleTransform, ScaleTransform.ScaleYProperty, targetScaleY, animationDuration, new ExponentialEase
            {
                EasingMode = EasingMode.EaseOut,
                Exponent = 5,
            });
            AnimateElementDimension(ExpandedContentTranslateTransform, TranslateTransform.YProperty, targetTranslateY, animationDuration, new CubicEase
            {
                EasingMode = EasingMode.EaseOut,
            });
        }
    }

    private void AnimateElementDimension(
        DependencyObject element,
        DependencyProperty property,
        double targetValue,
        int durationMilliseconds,
        IEasingFunction easingFunction)
    {
        var animationKey = GetAnimationKey(element, property);
        var animationVersion = GetNextAnimationVersion(animationKey);
        var animation = new DoubleAnimation
        {
            From = (double)element.GetValue(property),
            To = targetValue,
            Duration = TimeSpan.FromMilliseconds(durationMilliseconds),
            FillBehavior = FillBehavior.Stop,
            EasingFunction = easingFunction,
        };

        animation.Completed += (_, _) =>
        {
            if (!IsLatestAnimationVersion(animationKey, animationVersion))
            {
                return;
            }

            if (element is IAnimatable animatable)
            {
                animatable.BeginAnimation(property, null);
            }

            element.SetValue(property, targetValue);
        };

        if (element is IAnimatable animatableElement)
        {
            animatableElement.BeginAnimation(property, animation, HandoffBehavior.SnapshotAndReplace);
        }
    }

    private void CancelTrackedAnimation(DependencyObject element, DependencyProperty property)
    {
        var animationKey = GetAnimationKey(element, property);
        GetNextAnimationVersion(animationKey);

        if (element is IAnimatable animatable)
        {
            animatable.BeginAnimation(property, null);
        }
    }

    private static string GetAnimationKey(DependencyObject element, DependencyProperty property)
    {
        return $"{RuntimeHelpers.GetHashCode(element)}:{property.Name}";
    }

    private int GetNextAnimationVersion(string animationKey)
    {
        var nextVersion = _animationVersions.TryGetValue(animationKey, out var currentVersion)
            ? currentVersion + 1
            : 1;

        _animationVersions[animationKey] = nextVersion;
        return nextVersion;
    }

    private bool IsLatestAnimationVersion(string animationKey, int animationVersion)
    {
        return _animationVersions.TryGetValue(animationKey, out var currentVersion) &&
               currentVersion == animationVersion;
    }

    private void UpdateAnimatedNotchShape()
    {
        var scaleX = Math.Max(0.001, NotchScaleTransform.ScaleX);
        var scaleY = Math.Max(0.001, NotchScaleTransform.ScaleY);
        var radiusX = VisualBottomCornerRadius / scaleX;
        var radiusY = VisualBottomCornerRadius / scaleY;
        NotchBody.Clip = CreateNotchClipGeometry(NotchBody.ActualWidth, NotchBody.ActualHeight, radiusX, radiusY);
    }

    private void ApplyNotchStateWithoutFade(Action updateAction)
    {
        updateAction();
        Dispatcher.BeginInvoke(() =>
        {
            UpdateLayout();
            UpdateAnimatedNotchShape();
        }, DispatcherPriority.Render);
    }

    private void ApplyExpandedWindowState(Action updateAction)
    {
        NotchBody.Opacity = 0.0;
        updateAction();
        Dispatcher.BeginInvoke(() =>
        {
            UpdateLayout();
            NotchScaleTransform.ScaleX = GetCollapsedScaleX();
            NotchScaleTransform.ScaleY = GetCollapsedScaleY();
            UpdateAnimatedNotchShape();
            NotchBody.Opacity = 1.0;
            SettingsButton.Visibility = Visibility.Visible;
        }, DispatcherPriority.Render);
    }

    private void AnimateWindowDimension(
        DependencyProperty property,
        double targetValue,
        int durationMilliseconds,
        IEasingFunction easingFunction)
    {
        var animation = new DoubleAnimation
        {
            From = (double)GetValue(property),
            To = targetValue,
            Duration = TimeSpan.FromMilliseconds(durationMilliseconds),
            FillBehavior = FillBehavior.Stop,
            EasingFunction = easingFunction,
        };

        animation.Completed += (_, _) =>
        {
            BeginAnimation(property, null);
            SetCurrentValue(property, targetValue);
        };

        BeginAnimation(property, animation, HandoffBehavior.SnapshotAndReplace);
    }

    private static Geometry CreateNotchClipGeometry(double width, double height, double bottomRadiusX, double bottomRadiusY)
    {
        if (width <= 0 || height <= 0)
        {
            return Geometry.Empty;
        }

        var radiusX = Math.Min(bottomRadiusX, width / 2.0);
        var radiusY = Math.Min(bottomRadiusY, height);

        var geometry = new StreamGeometry();

        using (var context = geometry.Open())
        {
            context.BeginFigure(new Point(0, 0), isFilled: true, isClosed: true);
            context.LineTo(new Point(width, 0), isStroked: true, isSmoothJoin: false);
            context.LineTo(new Point(width, height - radiusY), isStroked: true, isSmoothJoin: false);
            context.ArcTo(
                new Point(width - radiusX, height),
                new Size(radiusX, radiusY),
                0,
                isLargeArc: false,
                SweepDirection.Clockwise,
                isStroked: true,
                isSmoothJoin: true);
            context.LineTo(new Point(radiusX, height), isStroked: true, isSmoothJoin: false);
            context.ArcTo(
                new Point(0, height - radiusY),
                new Size(radiusX, radiusY),
                0,
                isLargeArc: false,
                SweepDirection.Clockwise,
                isStroked: true,
                isSmoothJoin: true);
        }

        geometry.Freeze();
        return geometry;
    }

    private double GetCollapsedScaleX()
    {
        var collapsedBodyWidth = CollapsedWidth - (WindowHorizontalMargin * 2.0);
        var expandedBodyWidth = ExpandedWidth - (WindowHorizontalMargin * 2.0);
        return collapsedBodyWidth / expandedBodyWidth;
    }

    private double GetCollapsedScaleY()
    {
        var collapsedBodyHeight = CollapsedHeight - WindowBottomMargin;
        var expandedBodyHeight = _expandedWindowHeight - WindowBottomMargin;
        return expandedBodyHeight <= 0
            ? 1.0
            : collapsedBodyHeight / expandedBodyHeight;
    }

    private void PositionWindow()
    {
        Width = ExpandedWidth;
        Left = GetWindowLeft(ExpandedWidth);
        Top = GetWindowTop(overlayModeActive: ShouldDisplayOverlay(isInteractive: false));
        SettingsButton.Visibility = Visibility.Collapsed;
        UpdateAnimatedNotchShape();
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
}
