using System.Collections.ObjectModel;
using System.Numerics;
using System.Windows.Media.Media3D;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HelixToolkit;
using HelixToolkit.SharpDX;
using HelixToolkit.Wpf.SharpDX;
using TerrainBuilder.App.Services;
using TerrainBuilder.Core.Models;
using TerrainBuilder.Core.Services;

namespace TerrainBuilder.App.ViewModels;

public partial class SceneViewModel : ObservableObject
{
    private readonly IHelixMeshCache _meshCache;
    private readonly IStlParser _stlParser;
    private readonly GridSnapService _gridSnapService;

    public SceneViewModel(IHelixMeshCache meshCache, IStlParser stlParser, GridSnapService gridSnapService)
    {
        _meshCache = meshCache;
        _stlParser = stlParser;
        _gridSnapService = gridSnapService;
        EffectsManager = new DefaultEffectsManager();
        Camera = new PerspectiveCamera
        {
            Position = new Point3D(260, -320, 260),
            LookDirection = new Vector3D(-260, 320, -220),
            UpDirection = new Vector3D(0, 0, 1),
            NearPlaneDistance = 0.1,
            FarPlaneDistance = 10000
        };
        GridGeometry = CreateGridGeometry(25.4, 20);
    }

    public ObservableCollection<ScenePieceViewModel> Pieces { get; } = [];
    public IEffectsManager EffectsManager { get; }
    public Camera Camera { get; }
    public LineGeometry3D GridGeometry { get; }
    public double GridSizeMm { get; set; } = GridSnapService.OneInchMm;
    public int PieceCount => Pieces.Count;

    public event EventHandler? SceneChanged;
    public event EventHandler? ZoomExtentsRequested;
    public event EventHandler<Point3D>? FocusSelectedRequested;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteSelectedCommand))]
    [NotifyCanExecuteChangedFor(nameof(CopySelectedCommand))]
    [NotifyCanExecuteChangedFor(nameof(FocusSelectedCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveLeftCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveRightCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveForwardCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveBackCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveUpCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveDownCommand))]
    [NotifyCanExecuteChangedFor(nameof(RotateClockwiseCommand))]
    [NotifyCanExecuteChangedFor(nameof(RotateAnticlockwiseCommand))]
    private ScenePieceViewModel? selectedPiece;

    [ObservableProperty]
    private bool isLoading;

    public async Task<ScenePieceViewModel?> AddModelAsync(
        ModelLibraryItem model,
        PlacedTerrainPiece? placement = null,
        CancellationToken cancellationToken = default)
    {
        if (!model.IsValid) return null;
        IsLoading = true;
        try
        {
            var geometry = await _meshCache.GetAsync(model.FullPath, cancellationToken);
            var piece = new ScenePieceViewModel(model, geometry, placement);
            if (placement is null)
            {
                var column = Pieces.Count % 8;
                var row = Pieces.Count / 8;
                var snapped = _gridSnapService.Snap(
                    new TerrainVector3(column * GridSizeMm, row * GridSizeMm, 0),
                    GridSizeMm);
                piece.PositionX = snapped.X;
                piece.PositionY = snapped.Y;
            }

            Pieces.Add(piece);
            Select(piece);
            NotifySceneChanged();
            return piece;
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task<bool> RestorePieceAsync(
        PlacedTerrainPiece placement,
        ModelLibraryItem? indexedModel,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(placement.SourceStlPath)) return false;
        var model = indexedModel;
        if (model is null)
        {
            var info = new FileInfo(placement.SourceStlPath);
            model = new ModelLibraryItem
            {
                FileName = info.Name,
                FullPath = info.FullName,
                FolderPath = info.DirectoryName ?? string.Empty,
                FileSizeBytes = info.Length,
                LastModifiedUtc = info.LastWriteTimeUtc,
                Dimensions = await _stlParser.ReadDimensionsAsync(info.FullName, cancellationToken)
            };
        }

        return await AddModelAsync(model, placement, cancellationToken) is not null;
    }

    public void Select(ScenePieceViewModel? piece)
    {
        foreach (var item in Pieces) item.IsSelected = ReferenceEquals(item, piece);
        SelectedPiece = piece;
    }

    public void Clear()
    {
        Pieces.Clear();
        Select(null);
        NotifySceneChanged();
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void DeleteSelected()
    {
        if (SelectedPiece is null) return;
        Pieces.Remove(SelectedPiece);
        Select(null);
        NotifySceneChanged();
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void MoveLeft() => Nudge(-GridSizeMm, 0, 0);

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void MoveRight() => Nudge(GridSizeMm, 0, 0);

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void MoveForward() => Nudge(0, GridSizeMm, 0);

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void MoveBack() => Nudge(0, -GridSizeMm, 0);

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void MoveUp() => Nudge(0, 0, GridSizeMm);

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void MoveDown() => Nudge(0, 0, -GridSizeMm);

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void RotateClockwise() => Rotate(-90);

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void RotateAnticlockwise() => Rotate(90);

    [RelayCommand]
    private void FrameScene() => ZoomExtentsRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void FocusSelected()
    {
        if (SelectedPiece is not null)
        {
            FocusSelectedRequested?.Invoke(this, SelectedPiece.PivotPoint);
        }
    }

    private bool HasSelection() => SelectedPiece is not null;

    private void Nudge(double x, double y, double z)
    {
        if (SelectedPiece is null) return;
        SelectedPiece.PositionX += x;
        SelectedPiece.PositionY += y;
        SelectedPiece.PositionZ = Math.Max(0, SelectedPiece.PositionZ + z);
        NotifySceneChanged();
    }

    private void Rotate(double degrees)
    {
        if (SelectedPiece is null) return;
        SelectedPiece.RotationZ = (SelectedPiece.RotationZ + degrees) % 360;
        NotifySceneChanged();
    }

    private void NotifySceneChanged()
    {
        OnPropertyChanged(nameof(PieceCount));
        SceneChanged?.Invoke(this, EventArgs.Empty);
    }

    private static LineGeometry3D CreateGridGeometry(double spacing, int linesEachDirection)
    {
        var positions = new Vector3Collection();
        var indices = new IntCollection();
        var extent = spacing * linesEachDirection;
        for (var index = -linesEachDirection; index <= linesEachDirection; index++)
        {
            var coordinate = (float)(index * spacing);
            AddLine(positions, indices, new Vector3((float)-extent, coordinate, 0), new Vector3((float)extent, coordinate, 0));
            AddLine(positions, indices, new Vector3(coordinate, (float)-extent, 0), new Vector3(coordinate, (float)extent, 0));
        }

        return new LineGeometry3D { Positions = positions, Indices = indices };
    }

    private static void AddLine(Vector3Collection positions, IntCollection indices, Vector3 start, Vector3 end)
    {
        var offset = positions.Count;
        positions.Add(start);
        positions.Add(end);
        indices.Add(offset);
        indices.Add(offset + 1);
    }
}



