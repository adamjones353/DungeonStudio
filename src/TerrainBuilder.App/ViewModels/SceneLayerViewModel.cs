namespace TerrainBuilder.App.ViewModels;

public sealed record SceneLayerViewModel(
    int LevelNumber,
    double ElevationMm,
    int PieceCount)
{
    public string ShortName => Math.Abs(ElevationMm) < 0.01 ? "Ground" : $"Level {LevelNumber}";
    public string ElevationDisplay => $"{ElevationMm:0.##} mm";

    public string DisplayName => Math.Abs(ElevationMm) < 0.01
        ? $"Ground - 0 mm ({PieceCount})"
        : $"Level {LevelNumber} - {ElevationMm:0.##} mm ({PieceCount})";
}


