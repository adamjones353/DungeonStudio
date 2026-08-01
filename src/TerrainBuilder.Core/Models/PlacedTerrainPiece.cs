namespace TerrainBuilder.Core.Models;

public sealed record PlacedTerrainPiece
{
    public Guid InstanceId { get; init; } = Guid.NewGuid();
    public required string SourceStlPath { get; init; }
    public required string DisplayName { get; init; }
    public TerrainVector3 Position { get; init; } = TerrainVector3.Zero;
    public TerrainVector3 RotationDegrees { get; init; } = TerrainVector3.Zero;
    public TerrainVector3 Scale { get; init; } = new(1, 1, 1);
}
