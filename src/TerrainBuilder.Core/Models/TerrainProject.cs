namespace TerrainBuilder.Core.Models;

public sealed record TerrainProject
{
    public int FormatVersion { get; init; } = 1;
    public string Name { get; init; } = "Untitled Terrain";
    public string? LibraryFolder { get; init; }
    public double GridSizeMm { get; init; } = 25.4;
    public bool IsGridSnapEnabled { get; init; } = true;
    public int GridSnapPercentage { get; init; } = 100;
    public IReadOnlyList<PlacedTerrainPiece> Pieces { get; init; } = [];
}

