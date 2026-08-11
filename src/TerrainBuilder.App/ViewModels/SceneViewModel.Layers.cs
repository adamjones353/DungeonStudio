using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TerrainBuilder.Core.Services;

namespace TerrainBuilder.App.ViewModels;

public partial class SceneViewModel
{
    private bool isRebuildingLayers;

    public ObservableCollection<SceneLayerViewModel> Layers { get; } = [];

    public event EventHandler? LayerFilterChanged;

    [ObservableProperty]
    private bool showAllLayers = true;

    [ObservableProperty]
    private SceneLayerViewModel? activeLayer;

    [ObservableProperty]
    private double layerHeightMm = SceneLayerCalculator.DefaultLayerHeightMm;

    public int VisiblePieceCount => Pieces.Count(IsPieceVisible);
    public double ActiveLayerElevationMm => ActiveLayer?.ElevationMm ?? 0;
    public double PlacementElevationMm => ShowAllLayers ? 0 : ActiveLayerElevationMm;
    public string LayerStatus => ShowAllLayers
        ? $"All levels - {PieceCount:N0} piece(s)"
        : $"{ActiveLayer?.DisplayName ?? "Ground - 0 mm"}";

    public bool IsPieceVisible(ScenePieceViewModel piece) =>
        ShowAllLayers ||
        ActiveLayer is null ||
        GetPieceLevelElevation(piece) == ActiveLayer.ElevationMm;

    public void SetLayerView(bool showAll, double elevationMm)
    {
        RefreshLayerDefinitions(elevationMm);
        ShowAllLayers = showAll;
        ApplyLayerFilter();
    }

    private void InitializeLayerFiltering()
    {
        Layers.Add(new SceneLayerViewModel(1, 0, 0));
        ActiveLayer = Layers[0];
    }

    private void TrackPieceForLayers(ScenePieceViewModel piece) =>
        piece.PropertyChanged += PieceLayerPropertyChanged;

    private void UntrackPieceForLayers(ScenePieceViewModel piece) =>
        piece.PropertyChanged -= PieceLayerPropertyChanged;

    private void PieceLayerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not nameof(ScenePieceViewModel.PositionZ) and
            not nameof(ScenePieceViewModel.PlacementBaseElevation) ||
            sender is not ScenePieceViewModel piece)
        {
            return;
        }

        var preferredElevation = !ShowAllLayers && ReferenceEquals(piece, SelectedPiece)
            ? GetPieceLevelElevation(piece)
            : ActiveLayerElevationMm;
        RefreshLayerDefinitions(preferredElevation);
    }

    private void RefreshLayerDefinitions(double? preferredElevation = null)
    {
        var preferred = SceneLayerCalculator.GetLayerElevation(
            preferredElevation ?? ActiveLayer?.ElevationMm ?? 0,
            LayerHeightMm);
        var definitions = Pieces
            .GroupBy(GetPieceLevelElevation)
            .OrderBy(group => group.Key)
            .Select(group => new SceneLayerViewModel(
                SceneLayerCalculator.GetLevelNumber(group.Key, LayerHeightMm),
                group.Key,
                group.Count()))
            .ToList();

        if (definitions.Count == 0)
        {
            definitions.Add(new SceneLayerViewModel(1, 0, 0));
        }

        var selected = definitions.MinBy(layer => Math.Abs(layer.ElevationMm - preferred));
        isRebuildingLayers = true;
        try
        {
            Layers.Clear();
            foreach (var layer in definitions) Layers.Add(layer);
            ActiveLayer = selected;
        }
        finally
        {
            isRebuildingLayers = false;
        }

        ApplyLayerFilter();
    }

    partial void OnShowAllLayersChanged(bool value) => ApplyLayerFilter();

    partial void OnLayerHeightMmChanged(double value)
    {
        var normalized = SceneLayerCalculator.NormalizeLayerHeight(value);
        if (Math.Abs(normalized - value) > 0.0001)
        {
            LayerHeightMm = normalized;
            return;
        }

        RefreshLayerDefinitions();
    }

    partial void OnActiveLayerChanged(SceneLayerViewModel? value)
    {
        OnPropertyChanged(nameof(ActiveLayerElevationMm));
        OnPropertyChanged(nameof(PlacementElevationMm));
        OnPropertyChanged(nameof(LayerStatus));
        PreviousLayerCommand.NotifyCanExecuteChanged();
        NextLayerCommand.NotifyCanExecuteChanged();
        ShowSelectedLayerCommand.NotifyCanExecuteChanged();
        if (!isRebuildingLayers) ApplyLayerFilter();
    }

    partial void OnSelectedPieceChanged(ScenePieceViewModel? value)
    {
        ShowSelectedLayerCommand.NotifyCanExecuteChanged();
        if (value is null || !ShowAllLayers) return;
        var elevation = GetPieceLevelElevation(value);
        ActiveLayer = Layers.FirstOrDefault(layer => layer.ElevationMm == elevation) ?? ActiveLayer;
    }

    [RelayCommand(CanExecute = nameof(CanGoToPreviousLayer))]
    private void PreviousLayer()
    {
        var index = ActiveLayer is null ? 0 : Layers.IndexOf(ActiveLayer);
        if (index <= 0) return;
        ActiveLayer = Layers[index - 1];
        ShowAllLayers = false;
    }

    [RelayCommand(CanExecute = nameof(CanGoToNextLayer))]
    private void NextLayer()
    {
        var index = ActiveLayer is null ? -1 : Layers.IndexOf(ActiveLayer);
        if (index < 0 || index >= Layers.Count - 1) return;
        ActiveLayer = Layers[index + 1];
        ShowAllLayers = false;
    }

    [RelayCommand]
    private void IncreaseLayerHeight() =>
        LayerHeightMm = SceneLayerCalculator.NormalizeLayerHeight(LayerHeightMm + 5);

    [RelayCommand]
    private void DecreaseLayerHeight() =>
        LayerHeightMm = SceneLayerCalculator.NormalizeLayerHeight(LayerHeightMm - 5);

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void ShowSelectedLayer()
    {
        if (SelectedPiece is null) return;
        var elevation = GetPieceLevelElevation(SelectedPiece);
        ActiveLayer = Layers.FirstOrDefault(layer => layer.ElevationMm == elevation) ?? ActiveLayer;
        ShowAllLayers = false;
    }

    private bool CanGoToPreviousLayer() =>
        ActiveLayer is not null && Layers.IndexOf(ActiveLayer) > 0;

    private bool CanGoToNextLayer() =>
        ActiveLayer is not null && Layers.IndexOf(ActiveLayer) < Layers.Count - 1;

    private void ApplyLayerFilter()
    {
        if (SelectedPiece is not null && !IsPieceVisible(SelectedPiece))
        {
            Select(null);
        }

        OnPropertyChanged(nameof(VisiblePieceCount));
        OnPropertyChanged(nameof(PlacementElevationMm));
        OnPropertyChanged(nameof(LayerStatus));
        PreviousLayerCommand.NotifyCanExecuteChanged();
        NextLayerCommand.NotifyCanExecuteChanged();
        LayerFilterChanged?.Invoke(this, EventArgs.Empty);
    }

    private double GetPieceLevelElevation(ScenePieceViewModel piece) =>
        SceneLayerCalculator.GetLayerElevation(piece.PlacementBaseElevation, LayerHeightMm);
}

