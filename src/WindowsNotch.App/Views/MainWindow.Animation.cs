using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace WindowsNotch.App;

public partial class MainWindow
{
    private void HoverTimer_Tick(object? sender, EventArgs e)
    {
        UpdateAnimatedNotchShape();

        var now = DateTime.UtcNow;
        var cursorPoint = GetCursorPositionInDeviceIndependentPixels();
        var isDragInteractive =
            _isDragOver ||
            _isShareDropTargetActive ||
            _isShelfDropTargetActive;
        var isExpandedAreaInteractive =
            IsExpanded &&
            !_isCollapseAnimationActive &&
            IsCursorOverExpandedWindow(cursorPoint);
        var isPostDropHoldInteractive =
            _isWaitingForPostDropExit &&
            (isExpandedAreaInteractive || IsCursorInHotZone(cursorPoint));
        var isHoverInteractive =
            IsCursorInHotZone(cursorPoint) ||
            (!_isCollapseAnimationActive && IsCursorOverNotchBody(cursorPoint)) ||
            isExpandedAreaInteractive ||
            isPostDropHoldInteractive;
        var isInteractive = isDragInteractive || isHoverInteractive;

        if (_isWaitingForPostDropExit)
        {
            RefreshOverlayMode(isInteractive: true);

            if (isPostDropHoldInteractive || isDragInteractive)
            {
                _postDropExitStartedUtc = null;
                _lastInteractiveUtc = now;
                SetExpansionStage(NotchExpansionStage.Expanded);
                return;
            }

            _postDropExitStartedUtc ??= now;
            if (now - _postDropExitStartedUtc.Value < TimeSpan.FromMilliseconds(PostDropExitDelayMilliseconds))
            {
                return;
            }

            _isWaitingForPostDropExit = false;
            _postDropExitStartedUtc = null;
            _keepExpandedUntilUtc = null;
        }

        RefreshOverlayMode(isInteractive);

        if (isDragInteractive)
        {
            _hoverStartedUtc = null;
            _keepExpandedUntilUtc = null;
            _lastInteractiveUtc = now;
            SetExpansionStage(NotchExpansionStage.Expanded);
            return;
        }

        if (isHoverInteractive)
        {
            _hoverStartedUtc ??= now;
            _keepExpandedUntilUtc = null;
            _lastInteractiveUtc = now;

            var hoverDuration = now - _hoverStartedUtc.Value;
            var targetStage = hoverDuration >= TimeSpan.FromMilliseconds(PreviewExpandDelayMilliseconds)
                ? NotchExpansionStage.Expanded
                : NotchExpansionStage.Preview;

            SetExpansionStage(targetStage);
            return;
        }

        _hoverStartedUtc = null;

        if (!IsPresented)
        {
            return;
        }

        if (_keepExpandedUntilUtc is DateTime keepExpandedUntilUtc)
        {
            if (now < keepExpandedUntilUtc)
            {
                return;
            }

            _keepExpandedUntilUtc = null;
        }

        if (now - _lastInteractiveUtc < TimeSpan.FromMilliseconds(CollapseDelayMilliseconds))
        {
            return;
        }

        SetExpansionStage(NotchExpansionStage.Collapsed);
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

        var overlayModeActive = _pendingCollapseOverlayModeActive ?? ShouldDisplayOverlayAfterCollapse();
        _pendingCollapseOverlayModeActive = null;
        var collapsedTop = GetWindowTop(overlayModeActive);

        ApplyWindowBounds(GetWindowLeft(ExpandedWidth), collapsedTop, ExpandedWidth, CollapsedHeight);
        ApplyImmediateNotchScale(GetCollapsedScaleX(), 1.0);
        UpdateExpandedModePresentation();

        UpdateOverlayMode(overlayModeActive, immediateTopUpdate: true);
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

    private bool IsCursorOverExpandedWindow(Point cursorPoint)
    {
        var windowLeft = Left;
        var windowTop = Top;
        var windowRight = windowLeft + Width;
        var windowBottom = windowTop + Height;

        return cursorPoint.X >= windowLeft &&
               cursorPoint.X <= windowRight &&
               cursorPoint.Y >= windowTop &&
               cursorPoint.Y <= windowBottom;
    }

    private void SetExpansionStage(NotchExpansionStage stage)
    {
        if (stage == NotchExpansionStage.Collapsed &&
            _keepExpandedUntilUtc is DateTime keepExpandedUntilUtc &&
            DateTime.UtcNow < keepExpandedUntilUtc)
        {
            return;
        }

        if (_expansionStage == stage)
        {
            return;
        }

        var previousStage = _expansionStage;
        _expansionStage = stage;

        switch (stage)
        {
            case NotchExpansionStage.Preview:
                TransitionToPreview();
                return;
            case NotchExpansionStage.Expanded:
                TransitionToExpanded(previousStage);
                return;
            default:
                TransitionToCollapsed(previousStage);
                return;
        }
    }

    private void TransitionToPreview()
    {
        _isCollapseAnimationActive = false;
        _pendingCollapseOverlayModeActive = null;
        _collapseAnimationTimer.Stop();

        SettingsButton.Visibility = Visibility.Collapsed;
        UpdateExpandedModePresentation();
        HideExpandedContentImmediately();

        ApplyWindowBounds(GetWindowLeft(ExpandedWidth), GetWindowTop(overlayModeActive: true), ExpandedWidth, CollapsedHeight);
        RefreshOverlayMode(isInteractive: true, immediateTopUpdate: true);

        AnimateNotchScale(
            GetPreviewScaleX(),
            GetPreviewCollapsedScaleY(),
            ExpandAnimationMilliseconds);
    }

    private void TransitionToExpanded(NotchExpansionStage previousStage)
    {
        _isCollapseAnimationActive = false;
        _pendingCollapseOverlayModeActive = null;
        _collapseAnimationTimer.Stop();

        SettingsButton.Visibility = Visibility.Collapsed;
        UpdateExpandedModePresentation();
        HideExpandedContentImmediately();

        PrepareExpandedWindowForAnimation(
            previousStage == NotchExpansionStage.Preview ? GetPreviewScaleX() : GetCollapsedScaleX(),
            previousStage == NotchExpansionStage.Preview ? GetPreviewExpandedHostScaleY() : GetCollapsedScaleY());

        RefreshOverlayMode(isInteractive: true, immediateTopUpdate: true);
        AnimateNotchScale(1.0, 1.0, ExpandAnimationMilliseconds);
        AnimateExpandedContentIn(ExpandAnimationMilliseconds);
    }

    private void TransitionToCollapsed(NotchExpansionStage previousStage)
    {
        SettingsButton.Visibility = Visibility.Collapsed;
        UpdateExpandedModePresentation();

        if (previousStage == NotchExpansionStage.Expanded)
        {
            _isCollapseAnimationActive = true;
            _pendingCollapseOverlayModeActive = ShouldDisplayOverlayAfterCollapse();
            _collapseAnimationTimer.Stop();

            if (_pendingCollapseOverlayModeActive == false)
            {
                AnimateWindowDimension(TopProperty, GetWindowTop(overlayModeActive: false), CollapseAnimationMilliseconds, new CubicEase
                {
                    EasingMode = EasingMode.EaseOut,
                });
            }

            _collapseAnimationTimer.Start();
            HideExpandedContentImmediately();
            RefreshOverlayMode(
                isInteractive: _isDragOver || _isShareDropTargetActive || _isShelfDropTargetActive,
                immediateTopUpdate: false);
            AnimateNotchScale(GetCollapsedScaleX(), GetCollapsedScaleY(), CollapseAnimationMilliseconds);
            return;
        }

        _isCollapseAnimationActive = false;
        _pendingCollapseOverlayModeActive = null;
        _collapseAnimationTimer.Stop();

        HideExpandedContentImmediately();
        ApplyWindowBounds(GetWindowLeft(ExpandedWidth), GetWindowTop(overlayModeActive: true), ExpandedWidth, CollapsedHeight);
        RefreshOverlayMode(isInteractive: false, immediateTopUpdate: false);
        AnimateNotchScale(GetCollapsedScaleX(), 1.0, CollapseAnimationMilliseconds);
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
        NotchBody.Clip = CreateNotchClipGeometry(
            NotchBody.ActualWidth,
            NotchBody.ActualHeight,
            radiusX,
            radiusY);
    }

    private void PrepareExpandedWindowForAnimation(double collapsedScaleX, double collapsedScaleY)
    {
        ApplyWindowBounds(GetWindowLeft(ExpandedWidth), GetWindowTop(overlayModeActive: true), ExpandedWidth, _expandedWindowHeight);
        ExpandedContentViewport.Height = _expandedContentHeight;
        ExpandedContentScaleTransform.ScaleX = 0.97;
        ExpandedContentScaleTransform.ScaleY = 0.9;
        ExpandedContentTranslateTransform.Y = -6.0;
        ApplyImmediateNotchScale(collapsedScaleX, collapsedScaleY);
        SettingsButton.Visibility = Visibility.Visible;
        UpdateExpandedModePresentation();
    }

    private void ApplyImmediateNotchScale(double scaleX, double scaleY)
    {
        UpdateLayout();
        NotchScaleTransform.ScaleX = scaleX;
        NotchScaleTransform.ScaleY = scaleY;
        UpdateAnimatedNotchShape();
    }

    private void AnimateNotchScale(double targetScaleX, double targetScaleY, int durationMilliseconds)
    {
        AnimateElementDimension(NotchScaleTransform, ScaleTransform.ScaleXProperty, targetScaleX, durationMilliseconds, new QuinticEase
        {
            EasingMode = EasingMode.EaseOut,
        });
        AnimateElementDimension(NotchScaleTransform, ScaleTransform.ScaleYProperty, targetScaleY, durationMilliseconds, new ExponentialEase
        {
            EasingMode = EasingMode.EaseOut,
            Exponent = 5,
        });
    }

    private void AnimateExpandedContentIn(int durationMilliseconds)
    {
        AnimateElementDimension(ExpandedContentViewport, UIElement.OpacityProperty, 1.0, durationMilliseconds, new QuadraticEase
        {
            EasingMode = EasingMode.EaseOut,
        });
        AnimateElementDimension(ExpandedContentScaleTransform, ScaleTransform.ScaleXProperty, 1.0, durationMilliseconds, new QuinticEase
        {
            EasingMode = EasingMode.EaseOut,
        });
        AnimateElementDimension(ExpandedContentScaleTransform, ScaleTransform.ScaleYProperty, 1.0, durationMilliseconds, new ExponentialEase
        {
            EasingMode = EasingMode.EaseOut,
            Exponent = 5,
        });
        AnimateElementDimension(ExpandedContentTranslateTransform, TranslateTransform.YProperty, 0.0, durationMilliseconds, new CubicEase
        {
            EasingMode = EasingMode.EaseOut,
        });
    }

    private void ShowExpandedContentImmediately()
    {
        ExpandedContentViewport.Height = _expandedContentHeight;
        ExpandedContentViewport.Opacity = 1.0;
        ExpandedContentScaleTransform.ScaleX = 1.0;
        ExpandedContentScaleTransform.ScaleY = 1.0;
        ExpandedContentTranslateTransform.Y = 0.0;
    }

    private void HideExpandedContentImmediately()
    {
        CancelTrackedAnimation(ExpandedContentViewport, FrameworkElement.HeightProperty);
        CancelTrackedAnimation(ExpandedContentViewport, UIElement.OpacityProperty);
        CancelTrackedAnimation(ExpandedContentScaleTransform, ScaleTransform.ScaleXProperty);
        CancelTrackedAnimation(ExpandedContentScaleTransform, ScaleTransform.ScaleYProperty);
        CancelTrackedAnimation(ExpandedContentTranslateTransform, TranslateTransform.YProperty);

        ExpandedContentViewport.Height = 0.0;
        ExpandedContentViewport.Opacity = 0.0;
        ExpandedContentScaleTransform.ScaleX = 0.97;
        ExpandedContentScaleTransform.ScaleY = 0.9;
        ExpandedContentTranslateTransform.Y = -6.0;
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

    private static Geometry CreateNotchClipGeometry(
        double width,
        double height,
        double bottomRadiusX,
        double bottomRadiusY)
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

    private double GetPreviewScaleX()
    {
        var collapsedScaleX = GetCollapsedScaleX();
        return collapsedScaleX + ((1.0 - collapsedScaleX) * PreviewScaleProgress);
    }

    private static double GetPreviewCollapsedScaleY()
    {
        return PreviewHeightMultiplier;
    }

    private double GetPreviewExpandedHostScaleY()
    {
        return GetCollapsedScaleY() * PreviewHeightMultiplier;
    }

    private void PositionWindow()
    {
        Width = ExpandedWidth;
        Left = GetWindowLeft(ExpandedWidth);
        Top = GetWindowTop(overlayModeActive: ShouldDisplayOverlay(isInteractive: false));
        SettingsButton.Visibility = Visibility.Collapsed;
        ApplyImmediateNotchScale(GetCollapsedScaleX(), 1.0);
        RefreshOverlayModeForCurrentState();
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
