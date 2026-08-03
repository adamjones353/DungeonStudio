namespace TerrainBuilder.Core.Models;

public sealed record TerrainProject
{
    public int FormatVersion { get; init; } = 3;
    public string Name { get; init; } = "Untitled Terrain";
    public string? LibraryFolder { get; init; }
    public double GridSizeMm { get; init; } = 25.4;
    public bool IsGridSnapEnabled { get; init; } = true;
    public int GridSnapPercentage { get; init; } = 100;
    public double LayerHeightMm { get; init; } = 50;
    public bool ShowAllLayers { get; init; } = true;
    public double ActiveLayerElevationMm { get; init; }
    public IReadOnlyList<PlacedTerrainPiece> Pieces { get; init; } = [];
}





