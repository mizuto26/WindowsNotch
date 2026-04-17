using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WindowsNotch.App.Models;

namespace WindowsNotch.App;

public partial class MainWindow
{
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
        RefreshOverlayModeForCurrentState();
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
            _hoverStartedUtc = null;
            RefreshOverlayMode(isInteractive: true);
            SetExpansionStage(NotchExpansionStage.Expanded);
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
        RefreshOverlayModeForCurrentState();
        e.Handled = true;
    }

    private async void SharePanel_Drop(object sender, DragEventArgs e)
    {
        _isShareDropTargetActive = false;
        _isDragOver = false;
        UpdateDropZoneVisuals();
        KeepExpandedAfterDrop();
        e.Handled = true;

        if (e.Data.GetData(DataFormats.FileDrop) is not string[] entries || entries.Length == 0)
        {
            return;
        }

        try
        {
            var result = await _shelfService.SendEntriesToICloudAsync(entries);
            KeepExpandedAfterDrop();
            if (result.Success)
            {
                SetShareStatusSuccess();
            }
            else
            {
                SetShareStatusIdle();
            }
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
        RefreshOverlayModeForCurrentState();
        e.Handled = true;
    }

    private async void ShelfPanel_Drop(object sender, DragEventArgs e)
    {
        _isShelfDropTargetActive = false;
        _isDragOver = false;
        UpdateDropZoneVisuals();
        KeepExpandedAfterDrop();
        e.Handled = true;

        if (e.Data.GetData(DataFormats.FileDrop) is not string[] entries || entries.Length == 0)
        {
            return;
        }

        try
        {
            var updatedItems = await _shelfService.StashEntriesAsync(entries);
            ReplaceShelfItems(updatedItems);
            KeepExpandedAfterDrop();
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
            _hoverStartedUtc = null;
            RefreshOverlayMode(isInteractive: true);
            SetExpansionStage(NotchExpansionStage.Expanded);
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
        _topmostTimer.Stop();
        Topmost = false;
        UpdateWindowZOrder(HWND_NOTOPMOST);
        SetWindowClickThrough(isEnabled: true);
        RefreshOverlayMode(isInteractive: true, immediateTopUpdate: true);
        SetExpansionStage(NotchExpansionStage.Expanded);

        try
        {
            DragDrop.DoDragDrop(ShelfList, data, DragDropEffects.Copy | DragDropEffects.Move);
        }
        finally
        {
            SetWindowClickThrough(isEnabled: false);
            RefreshOverlayModeForCurrentState(immediateTopUpdate: true);
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
            ? new SolidColorBrush(Color.FromArgb(54, 72, 137, 255))
            : new SolidColorBrush(Color.FromArgb(255, 0, 0, 0));

        ShareDropZone.BorderBrush = _isShareDropTargetActive
            ? new SolidColorBrush(Color.FromArgb(120, 126, 184, 255))
            : Brushes.Transparent;

        EmptyShelfPanel.Background = _isShelfDropTargetActive
            ? new SolidColorBrush(Color.FromArgb(54, 72, 137, 255))
            : new SolidColorBrush(Color.FromArgb(255, 0, 0, 0));

        EmptyShelfPanel.BorderBrush = _isShelfDropTargetActive
            ? new SolidColorBrush(Color.FromArgb(120, 126, 184, 255))
            : Brushes.Transparent;

        ShelfPanel.BorderBrush = _isShelfDropTargetActive
            ? new SolidColorBrush(Color.FromArgb(90, 102, 209, 255))
            : Brushes.Transparent;
    }

    private void RecalculateExpandedLayout()
    {
        ExpandedContentRoot.Measure(new Size(ExpandedWidth - 32, double.PositiveInfinity));
        _expandedContentHeight = ExpandedContentRoot.DesiredSize.Height;
        _expandedWindowHeight = CollapsedHeight + ExpandedContentTopSpacing + _expandedContentHeight;

        if (IsExpanded)
        {
            ShowExpandedContentImmediately();
            Height = _expandedWindowHeight;
        }
        else
        {
            HideExpandedContentImmediately();
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
}
