using TerrainBuilder.Core.Models;
using TerrainBuilder.Infrastructure.Projects;

namespace TerrainBuilder.Tests;

public sealed class JsonProjectStoreTests
{
    [Fact]
    public async Task SaveAndLoad_RoundTripsBasicProject()
    {
        using var folder = new TemporaryFolder();
        var path = Path.Combine(folder.Path, "room.terrainproject");
        var source = new TerrainProject
        {
            Name = "Room One",
            LibraryFolder = @"C:\Models",
            IsGridSnapEnabled = true,
            LayerHeightMm = 50,
            ShowAllLayers = false,
            ActiveLayerElevationMm = 76.2,
            Pieces =
            [
                new PlacedTerrainPiece
                {
                    SourceStlPath = @"C:\Models\floor.stl",
                    DisplayName = "Floor",
                    Position = new TerrainVector3(25.4, 50.8, 0),
                    BaseElevationMm = 0,
                    RotationDegrees = new TerrainVector3(0, 0, 90)
                }
            ]
        };
        var store = new JsonProjectStore();

        await store.SaveAsync(path, source);
        var loaded = await store.LoadAsync(path);

        Assert.Equal("Room One", loaded.Name);
        Assert.True(loaded.IsGridSnapEnabled);
        Assert.Equal(50, loaded.LayerHeightMm);
        Assert.False(loaded.ShowAllLayers);
        Assert.Equal(76.2, loaded.ActiveLayerElevationMm);
        Assert.Single(loaded.Pieces);
        Assert.Equal(0, loaded.Pieces[0].BaseElevationMm);
        Assert.Equal(90, loaded.Pieces[0].RotationDegrees.Z);
    }

    [Fact]
    public async Task Load_IgnoresLegacyGridSnapPercentage()
    {
        using var folder = new TemporaryFolder();
        var path = Path.Combine(folder.Path, "legacy.terrainproject");
        await File.WriteAllTextAsync(path, """
            {
              "FormatVersion": 3,
              "Name": "Legacy Room",
              "IsGridSnapEnabled": true,
              "GridSnapPercentage": 50,
              "Pieces": []
            }
            """);

        var loaded = await new JsonProjectStore().LoadAsync(path);

        Assert.Equal("Legacy Room", loaded.Name);
        Assert.True(loaded.IsGridSnapEnabled);
    }
}


