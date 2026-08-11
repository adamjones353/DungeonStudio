using System.ComponentModel;
using System.Numerics;
using System.Windows.Input;
using System.Windows.Media.Media3D;
using HelixToolkit.SharpDX;
using HelixToolkit.Wpf.SharpDX;
using SharpDX.Direct3D11;
using TerrainBuilder.App.ViewModels;

namespace TerrainBuilder.App;

public partial class MainWindow
{
    private readonly List<InstancingMeshGeometryModel3D> instancedSceneModels = [];
    private ItemsModel3D? selectedPieceHost;

    private void InitializeInstancedSceneRendering()
    {
        selectedPieceHost = TerrainViewport.Items.OfType<ItemsModel3D>().FirstOrDefault();
        TerrainViewport.PreviewMouseLeftButtonDown += InstancedSceneMouseDown;
        ViewModel.Scene.SceneChanged += (_, _) => RefreshInstancedScene();
        ViewModel.Scene.LayerFilterChanged += (_, _) => RefreshInstancedScene();
        ViewModel.Scene.PropertyChanged += ScenePropertyChanged;
        RefreshInstancedScene();
    }

    private void ScenePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SceneViewModel.SelectedPiece))
        {
            RefreshInstancedScene();
        }
    }

    private void RefreshInstancedScene()
    {
        if (selectedPieceHost is null) return;

        selectedPieceHost.ItemsSource = ViewModel.Scene.SelectedPiece is { } selected &&
                                        ViewModel.Scene.IsPieceVisible(selected)
            ? new[] { selected }
            : Array.Empty<ScenePieceViewModel>();

        foreach (var model in instancedSceneModels)
        {
            TerrainViewport.Items.Remove(model);
            model.Dispose();
        }
        instancedSceneModels.Clear();

        var groups = ViewModel.Scene.Pieces
            .Where(piece => ViewModel.Scene.IsPieceVisible(piece) &&
                            !ReferenceEquals(piece, ViewModel.Scene.SelectedPiece))
            .GroupBy(piece => piece.Source.FullPath, StringComparer.OrdinalIgnoreCase);

        foreach (var group in groups)
        {
            var pieces = group.ToArray();
            var model = new InstancingMeshGeometryModel3D
            {
                DataContext = pieces,
                Geometry = pieces[0].Geometry,
                Material = PhongMaterials.Pewter,
                Instances = pieces.Select(piece => ToInstanceMatrix(piece.Transform.Value)).ToArray(),
                InstanceIdentifiers = pieces.Select(piece => piece.InstanceId).ToArray(),
                CullMode = CullMode.Back,
                IsThrowingShadow = false
            };
            instancedSceneModels.Add(model);
            TerrainViewport.Items.Add(model);
        }
    }

    private void InstancedSceneMouseDown(object sender, MouseButtonEventArgs e)
    {
        var mousePoint = e.GetPosition(TerrainViewport);
        var hit = TerrainViewport.FindHits(mousePoint)
            .Where(result => result.IsValid && ResolveScenePiece(result) is not null)
            .MinBy(result => result.Distance);
        var piece = ResolveScenePiece(hit);
        if (piece is null) return;

        var worldPoint = TerrainViewport.UnProjectOnPlane(
            mousePoint,
            new Point3D(0, 0, piece.PositionZ),
            new Vector3D(0, 0, 1));
        if (worldPoint is null) return;

        UpdateMousePastePosition(mousePoint);
        ViewModel.Scene.Select(piece);
        draggedPiece = piece;
        pieceDragChanged = false;
        dragPlaneZ = piece.PositionZ;
        dragBaseElevationZ = piece.PlacementBaseElevation;
        dragOffsetX = piece.PositionX - worldPoint.Value.X;
        dragOffsetY = piece.PositionY - worldPoint.Value.Y;
        TerrainViewport.CaptureMouse();
        e.Handled = true;
    }

    private ScenePieceViewModel? ResolveScenePiece(HitTestResult? hit)
    {
        if (hit?.ModelHit is Element3D { DataContext: ScenePieceViewModel piece })
        {
            return piece;
        }

        if (hit?.ModelHit is not InstancingMeshGeometryModel3D
            {
                DataContext: ScenePieceViewModel[] instances
            })
        {
            return null;
        }

        if (hit.Tag is int index && index >= 0 && index < instances.Length)
        {
            return instances[index];
        }

        if (hit.Tag is Guid instanceId)
        {
            return instances.FirstOrDefault(piece => piece.InstanceId == instanceId);
        }

        return null;
    }

    private static Matrix4x4 ToInstanceMatrix(Matrix3D matrix) => new(
        (float)matrix.M11, (float)matrix.M12, (float)matrix.M13, (float)matrix.M14,
        (float)matrix.M21, (float)matrix.M22, (float)matrix.M23, (float)matrix.M24,
        (float)matrix.M31, (float)matrix.M32, (float)matrix.M33, (float)matrix.M34,
        (float)matrix.OffsetX, (float)matrix.OffsetY, (float)matrix.OffsetZ, (float)matrix.M44);
}
