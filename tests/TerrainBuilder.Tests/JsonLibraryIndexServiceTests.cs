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

    [Fact]
    public async Task Scan_ReportsCurrentFileAndFinishesAtOneHundredPercent()
    {
        using var folder = new TemporaryFolder();
        var firstPath = Path.Combine(folder.Path, "first.stl");
        var secondPath = Path.Combine(folder.Path, "second.stl");
        await File.WriteAllTextAsync(firstPath, "placeholder");
        await File.WriteAllTextAsync(secondPath, "placeholder");
        var updates = new List<LibraryScanProgress>();
        var service = new JsonLibraryIndexService(
            new CountingParser(),
            Path.Combine(folder.Path, "cache", "index.json"));

        await service.ScanAsync(folder.Path, new SynchronousProgress<LibraryScanProgress>(updates.Add));

        Assert.Contains(updates, update => update.CurrentFilePath == firstPath);
        Assert.Contains(updates, update => update.CurrentFilePath == secondPath);
        Assert.Equal(2, updates
            .SelectMany(update => update.CompletedItems ?? [])
            .Select(item => item.FullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count());
        Assert.Equal(100, updates[^1].Percentage);
        Assert.Equal(2, updates[^1].CompletedFiles);
        Assert.Equal(2, updates[^1].TotalFiles);
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

    private sealed class SynchronousProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
