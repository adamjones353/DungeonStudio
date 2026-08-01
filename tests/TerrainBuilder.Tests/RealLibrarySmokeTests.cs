using TerrainBuilder.Infrastructure.Library;
using TerrainBuilder.Infrastructure.Stl;

namespace TerrainBuilder.Tests;

public sealed class RealLibrarySmokeTests
{
    [Fact]
    public async Task Scan_WhenRealLibraryIsProvided_IndexesTheCollection()
    {
        var libraryPath = Environment.GetEnvironmentVariable("TERRAIN_BUILDER_TEST_LIBRARY");
        if (string.IsNullOrWhiteSpace(libraryPath)) return;

        using var cache = new TemporaryFolder();
        var service = new JsonLibraryIndexService(new StlParser(), Path.Combine(cache.Path, "index.json"));

        var models = await service.ScanAsync(libraryPath);

        Assert.True(models.Count >= 400, $"Expected at least 400 STL files but found {models.Count}.");
        Assert.Contains(models, model => model.IsValid && model.Dimensions.WidthMm > 0);
    }
}
