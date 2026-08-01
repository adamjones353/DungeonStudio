using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Media3D;
using HelixToolkit.SharpDX;
using HelixToolkit.Wpf.SharpDX;
using TerrainBuilder.App.ViewModels;

namespace TerrainBuilder.App;

public partial class MainWindow
{
    private ScenePieceViewModel? draggedPiece;
    private double dragOffsetX;
    private double dragOffsetY;
    private double dragPlaneZ;
    private bool pieceDragChanged;
    private bool isCameraPanning;
    private Point previousPanPoint;

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        TerrainViewport.EnableRenderFrustum = true;
        TerrainViewport.EnableAutoOctreeUpdate = false;
        TerrainViewport.EnableMouseButtonHitTest = false;
        TerrainViewport.EnableDpiScale = false;
        TerrainViewport.IsInertiaEnabled = false;
        TerrainViewport.CameraInertiaFactor = 0;
        TerrainViewport.InfiniteSpin = false;
        TerrainViewport.SpinReleaseTime = 0;
        TerrainViewport.MSAA = MSAALevel.Disable;
        TerrainViewport.PreviewMouseLeftButtonDown += TerrainViewportOnLeftButtonDown;
        TerrainViewport.PreviewMouseMove += TerrainViewportOnMouseMove;
        TerrainViewport.PreviewMouseLeftButtonUp += TerrainViewportOnLeftButtonUp;
        TerrainViewport.PreviewMouseDown += TerrainViewportOnMiddleButtonDown;
        TerrainViewport.PreviewMouseUp += TerrainViewportOnMiddleButtonUp;
        TerrainViewport.MouseRightButtonUp += TerrainViewportOnRightButtonUp;
        TerrainViewport.LostMouseCapture += TerrainViewportOnLostMouseCapture;
        PreviewKeyDown += MainWindowOnPreviewKeyDown;
        PreviewKeyUp += MainWindowOnPreviewKeyUp;
    }

    private void TerrainViewportOnLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var mousePoint = e.GetPosition(TerrainViewport);
        UpdateMousePastePosition(mousePoint);
        var hit = TerrainViewport.FindHits(mousePoint)
            .FirstOrDefault(result => result.ModelHit is Element3D element &&
                                      element.DataContext is ScenePieceViewModel);

        if (hit?.ModelHit is not Element3D hitElement ||
            hitElement.DataContext is not ScenePieceViewModel piece)
        {
            return;
        }

        var worldPoint = TerrainViewport.UnProjectOnPlane(
            mousePoint,
            new Point3D(0, 0, piece.PositionZ),
            new Vector3D(0, 0, 1));
        if (worldPoint is null) return;

        ViewModel.Scene.Select(piece);
        draggedPiece = piece;
        pieceDragChanged = false;
        dragPlaneZ = piece.PositionZ;
        dragOffsetX = piece.PositionX - worldPoint.Value.X;
        dragOffsetY = piece.PositionY - worldPoint.Value.Y;
        TerrainViewport.CaptureMouse();
        e.Handled = true;
    }

    private void TerrainViewportOnMouseMove(object sender, MouseEventArgs e)
    {
        var currentPoint = e.GetPosition(TerrainViewport);

        if (isCameraPanning)
        {
            if (e.MiddleButton != MouseButtonState.Pressed)
            {
                EndCameraPan();
                return;
            }

            PanCamera(currentPoint);
            e.Handled = true;
            return;
        }

        if (draggedPiece is not null && e.LeftButton == MouseButtonState.Pressed)
        {
            var worldPoint = TerrainViewport.UnProjectOnPlane(
                currentPoint,
                new Point3D(0, 0, dragPlaneZ),
                new Vector3D(0, 0, 1));
            if (worldPoint is null) return;

            ViewModel.Scene.MoveSelectedTo(
                worldPoint.Value.X + dragOffsetX,
                worldPoint.Value.Y + dragOffsetY,
                dragPlaneZ,
                commitSceneChange: false);
            pieceDragChanged = true;
            e.Handled = true;
            return;
        }

        if (e.LeftButton == MouseButtonState.Released &&
            e.MiddleButton == MouseButtonState.Released &&
            e.RightButton == MouseButtonState.Released)
        {
            UpdateMousePastePosition(currentPoint);
        }
    }

    private void MainWindowOnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.V &&
            Keyboard.Modifiers.HasFlag(ModifierKeys.Control) &&
            TerrainViewport.IsMouseOver)
        {
            UpdateMousePastePosition(Mouse.GetPosition(TerrainViewport));
        }
    }

    private void MainWindowOnPreviewKeyUp(object sender, KeyEventArgs e) => TerrainViewport.StopSpin();

    private void TerrainViewportOnLeftButtonUp(object sender, MouseButtonEventArgs e) => EndPieceDrag();

    private void TerrainViewportOnMiddleButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Middle) return;
        EndPieceDrag();
        previousPanPoint = e.GetPosition(TerrainViewport);
        isCameraPanning = true;
        TerrainViewport.CaptureMouse();
        e.Handled = true;
    }

    private void TerrainViewportOnMiddleButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Middle) return;
        EndCameraPan();
        e.Handled = true;
    }

    private void TerrainViewportOnRightButtonUp(object sender, MouseButtonEventArgs e) => TerrainViewport.StopSpin();

    private void TerrainViewportOnLostMouseCapture(object sender, MouseEventArgs e)
    {
        draggedPiece = null;
        pieceDragChanged = false;
        isCameraPanning = false;
        TerrainViewport.StopSpin();
    }

    private void EndPieceDrag()
    {
        if (draggedPiece is null) return;
        var shouldCommit = pieceDragChanged;
        draggedPiece = null;
        pieceDragChanged = false;
        if (shouldCommit) ViewModel.Scene.CommitInteractiveChange();
        if (TerrainViewport.IsMouseCaptured) TerrainViewport.ReleaseMouseCapture();
    }

    private void UpdateMousePastePosition(Point viewportPoint)
    {
        var worldPoint = TerrainViewport.UnProjectOnPlane(
            viewportPoint,
            new Point3D(0, 0, 0),
            new Vector3D(0, 0, 1));
        if (worldPoint is not null)
        {
            ViewModel.Scene.SetMousePastePosition(worldPoint.Value.X, worldPoint.Value.Y);
        }
    }

    private void EndCameraPan()
    {
        if (!isCameraPanning) return;
        isCameraPanning = false;
        TerrainViewport.StopSpin();
        if (TerrainViewport.IsMouseCaptured) TerrainViewport.ReleaseMouseCapture();
    }

    private void PanCamera(Point currentPoint)
    {
        var deltaX = currentPoint.X - previousPanPoint.X;
        var deltaY = currentPoint.Y - previousPanPoint.Y;
        previousPanPoint = currentPoint;
        if (Math.Abs(deltaX) < double.Epsilon && Math.Abs(deltaY) < double.Epsilon) return;

        var camera = ViewModel.Scene.Camera;
        var forward = camera.LookDirection;
        if (forward.LengthSquared < 0.000001) return;
        var distance = forward.Length;
        forward.Normalize();

        var right = Vector3D.CrossProduct(forward, camera.UpDirection);
        if (right.LengthSquared < 0.000001) return;
        right.Normalize();
        var screenUp = Vector3D.CrossProduct(right, forward);
        screenUp.Normalize();

        var unitsPerPixel = distance / Math.Max(TerrainViewport.ActualHeight, 1) * 1.5;
        var translation = right * (-deltaX * unitsPerPixel) + screenUp * (deltaY * unitsPerPixel);
        camera.Position += translation;
    }

    private void LibraryTreeSelectionChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        ViewModel.Library.SelectedNode = e.NewValue as LibraryTreeNodeViewModel;
    }

    private async void ThumbnailImageLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: LibraryTreeNodeViewModel node })
        {
            await node.EnsureThumbnailAsync();
        }
    }
}
