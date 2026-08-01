using System.Numerics;
using TerrainBuilder.Core.Models;
using TerrainBuilder.Core.Services;
using TerrainBuilder.Infrastructure.Library;

namespace TerrainBuilder.Tests;

public sealed class JsonLibraryIndexServiceTests
{
    [Fact]
    public async Task Scan_ReusesUnchangedCachedMetadataAndRescansChangedFile()
    {
        using var folder = new TemporaryFolder();
        var stlPath = Path.Combine(folder.Path, "tile.stl");
        await File.WriteAllTextAsync(stlPath, "placeholder");
        var parser = new CountingParser();
        var indexPath = Path.Combine(folder.Path, "cache", "index.json");
        var service = new JsonLibraryIndexService(parser, indexPath);

        var first = await service.ScanAsync(folder.Path);
        var second = await service.ScanAsync(folder.Path);
        await File.AppendAllTextAsync(stlPath, "changed");
        var third = await service.ScanAsync(folder.Path);

        Assert.Single(first);
        Assert.Single(second);
        Assert.Single(third);
        Assert.Equal(2, parser.DimensionReads);
    }

    private sealed class CountingParser : IStlParser
    {
        public int DimensionReads { get; private set; }

        public Task<ModelDimensions> ReadDimensionsAsync(string filePath, CancellationToken cancellationToken = default)
        {
            DimensionReads++;
            return Task.FromResult(new ModelDimensions(25.4, 25.4, 6));
        }

        public Task<StlMeshData> LoadMeshAsync(string filePath, CancellationToken cancellationToken = default) =>
            Task.FromResult(new StlMeshData([], [], [], ModelDimensions.Empty));
    }
}
