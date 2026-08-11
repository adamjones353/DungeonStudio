using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using HelixToolkit.Wpf.SharpDX;
using TerrainBuilder.App.ViewModels;

namespace TerrainBuilder.App;

public partial class MainWindow
{
    private const string LibraryModelPathFormat = "TerrainBuilder.LibraryModelPath";
    private Point libraryDragStart;
    private string? libraryDragPath;

    private void LibraryTreePreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        libraryDragStart = e.GetPosition(null);
        var container = FindVisualParent<TreeViewItem>(e.OriginalSource as DependencyObject);
        libraryDragPath = (container?.DataContext as LibraryTreeNodeViewModel)?.Model?.FullPath;
    }

    private void LibraryTreePreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || libraryDragPath is null) return;

        var current = e.GetPosition(null);
        if (Math.Abs(current.X - libraryDragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(current.Y - libraryDragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        var path = libraryDragPath;
        libraryDragPath = null;
        var data = new DataObject();
        data.SetData(LibraryModelPathFormat, path);
        e.Handled = true;
        DragDrop.DoDragDrop((DependencyObject)sender, data, DragDropEffects.Copy);
    }

    private void TerrainViewportDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(LibraryModelPathFormat)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private async void TerrainViewportDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(LibraryModelPathFormat) is not string modelPath) return;
        var model = ViewModel.Library.Models.FirstOrDefault(item =>
            string.Equals(item.FullPath, modelPath, StringComparison.OrdinalIgnoreCase));
        if (model is null) return;

        var placementElevation = ViewModel.Scene.PlacementElevationMm;
        var worldPoint = TerrainViewport.UnProjectOnPlane(
            e.GetPosition(TerrainViewport),
            new Point3D(0, 0, placementElevation),
            new Vector3D(0, 0, 1));
        if (worldPoint is null) return;

        e.Handled = true;
        ViewModel.IsBusy = true;
        ViewModel.StatusMessage = $"Loading {model.DisplayName}...";
        try
        {
            var piece = await ViewModel.Scene.AddModelAsync(model);
            if (piece is null)
            {
                ViewModel.StatusMessage = "That STL could not be placed.";
                return;
            }

            ViewModel.Scene.MoveSelectedTo(
                worldPoint.Value.X,
                worldPoint.Value.Y,
                placementElevation,
                usePrecisionSnap: Keyboard.Modifiers.HasFlag(ModifierKeys.Control));
            ViewModel.StatusMessage = ViewModel.Scene.IsGridSnapEnabled
                ? $"Placed {model.DisplayName} at the nearest snap point."
                : $"Placed {model.DisplayName}.";
        }
        catch (Exception exception)
        {
            ViewModel.StatusMessage = $"Could not place {model.DisplayName}: {exception.Message}";
        }
        finally
        {
            ViewModel.IsBusy = false;
        }
    }

    private static T? FindVisualParent<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match) return match;
            current = current is Visual or Visual3D
                ? VisualTreeHelper.GetParent(current)
                : LogicalTreeHelper.GetParent(current);
        }

        return null;
    }
}


