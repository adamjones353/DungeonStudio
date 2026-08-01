using TerrainBuilder.Core.Models;
using TerrainBuilder.Core.Services;

namespace TerrainBuilder.Tests;

public sealed class PrintListServiceTests
{
    [Fact]
    public void Generate_GroupsOnlyByFullSourcePath()
    {
        var pathA = Path.GetFullPath(Path.Combine("LibraryA", "wall.stl"));
        var pathB = Path.GetFullPath(Path.Combine("LibraryB", "wall.stl"));
        var pieces = new[]
        {
            Piece(pathA), Piece(pathA), Piece(pathB)
        };
        var library = new[]
        {
            Model(pathA, new ModelDimensions(25.4, 12.7, 50)),
            Model(pathB, new ModelDimensions(50.8, 12.7, 50))
        };

        var result = new PrintListService().Generate(pieces, library);

        Assert.Equal(2, result.Count);
        Assert.Equal(2, result.Single(item => item.FullPath == pathA).Quantity);
        Assert.Equal(1, result.Single(item => item.FullPath == pathB).Quantity);
    }

    private static PlacedTerrainPiece Piece(string path) => new()
    {
        SourceStlPath = path,
        DisplayName = "wall"
    };

    private static ModelLibraryItem Model(string path, ModelDimensions dimensions) => new()
    {
        FileName = Path.GetFileName(path),
        FullPath = path,
        FolderPath = Path.GetDirectoryName(path)!,
        Dimensions = dimensions,
        LastModifiedUtc = DateTime.UtcNow
    };
}
