namespace TerrainBuilder.Core.Models;

public sealed record PrintListItem
{
    public required string ModelName { get; init; }
    public required string FullPath { get; init; }
    public required string SourceFolder { get; init; }
    public required ModelDimensions Dimensions { get; init; }
    public int Quantity { get; init; }
}
