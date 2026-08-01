using System.Windows.Media.Media3D;
using CommunityToolkit.Mvvm.ComponentModel;
using HelixToolkit.SharpDX;
using HelixToolkit.Wpf.SharpDX;
using TerrainBuilder.Core.Models;

namespace TerrainBuilder.App.ViewModels;

public partial class ScenePieceViewModel : ObservableObject
{
    private static readonly Material NeutralMaterial = PhongMaterials.Pewter;
    private static readonly Material SelectedMaterial = PhongMaterials.Gold;
    private readonly double rotationCentreX;
    private readonly double rotationCentreY;

    public ScenePieceViewModel(ModelLibraryItem source, MeshGeometry3D geometry, PlacedTerrainPiece? placement = null)
    {
        Source = source;
        Geometry = geometry;
        var positions = geometry.Positions ?? throw new InvalidDataException("The STL mesh contains no positions.");
        rotationCentreX = (positions.Min(vertex => vertex.X) + positions.Max(vertex => vertex.X)) / 2;
        rotationCentreY = (positions.Min(vertex => vertex.Y) + positions.Max(vertex => vertex.Y)) / 2;
        InstanceId = placement?.InstanceId ?? Guid.NewGuid();
        DisplayName = placement?.DisplayName ?? source.DisplayName;
        var position = placement?.Position ?? TerrainVector3.Zero;
        var rotation = placement?.RotationDegrees ?? TerrainVector3.Zero;
        var scale = placement?.Scale ?? new TerrainVector3(1, 1, 1);
        positionX = position.X;
        positionY = position.Y;
        positionZ = position.Z;
        rotationZ = rotation.Z;
        scaleX = scale.X;
        scaleY = scale.Y;
        scaleZ = scale.Z;
        UpdateTransform();
    }

    public Guid InstanceId { get; }
    public string DisplayName { get; }
    public ModelLibraryItem Source { get; }
    public MeshGeometry3D Geometry { get; }
    public ModelDimensions Dimensions => Source.Dimensions;
    public Point3D PivotPoint => new(
        rotationCentreX * ScaleX + PositionX,
        rotationCentreY * ScaleY + PositionY,
        PositionZ + Dimensions.HeightMm * ScaleZ / 2);

    [ObservableProperty]
    private double positionX;

    [ObservableProperty]
    private double positionY;

    [ObservableProperty]
    private double positionZ;

    [ObservableProperty]
    private double rotationZ;

    [ObservableProperty]
    private double scaleX = 1;

    [ObservableProperty]
    private double scaleY = 1;

    [ObservableProperty]
    private double scaleZ = 1;

    [ObservableProperty]
    private bool isSelected;

    [ObservableProperty]
    private Transform3D transform = Transform3D.Identity;

    public Material Material => IsSelected ? SelectedMaterial : NeutralMaterial;
    public string? PostEffects => IsSelected ? "selectionHighlight[color:#FFDBAA59]" : null;

    partial void OnPositionXChanged(double value) => UpdateTransform();
    partial void OnPositionYChanged(double value) => UpdateTransform();
    partial void OnPositionZChanged(double value) => UpdateTransform();
    partial void OnRotationZChanged(double value) => UpdateTransform();
    partial void OnScaleXChanged(double value) => UpdateTransform();
    partial void OnScaleYChanged(double value) => UpdateTransform();
    partial void OnScaleZChanged(double value) => UpdateTransform();
    partial void OnIsSelectedChanged(bool value)
    {
        OnPropertyChanged(nameof(Material));
        OnPropertyChanged(nameof(PostEffects));
    }

    public PlacedTerrainPiece ToModel() => new()
    {
        InstanceId = InstanceId,
        SourceStlPath = Source.FullPath,
        DisplayName = DisplayName,
        Position = new TerrainVector3(PositionX, PositionY, PositionZ),
        RotationDegrees = new TerrainVector3(0, 0, RotationZ),
        Scale = new TerrainVector3(ScaleX, ScaleY, ScaleZ)
    };

    private void UpdateTransform()
    {
        var group = new Transform3DGroup();
        group.Children.Add(new ScaleTransform3D(ScaleX, ScaleY, ScaleZ));
        group.Children.Add(new RotateTransform3D(
            new AxisAngleRotation3D(new Vector3D(0, 0, 1), RotationZ),
            rotationCentreX * ScaleX,
            rotationCentreY * ScaleY,
            0));
        group.Children.Add(new TranslateTransform3D(PositionX, PositionY, Math.Max(0, PositionZ)));
        Transform = group;
    }
}



