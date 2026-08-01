using TerrainBuilder.Core.Models;
using TerrainBuilder.Infrastructure.Settings;

namespace TerrainBuilder.Tests;

public sealed class JsonAppSettingsStoreTests
{
    [Fact]
    public async Task SaveAndLoad_RoundTripsLastLibraryFolder()
    {
        using var folder = new TemporaryFolder();
        var settingsPath = Path.Combine(folder.Path, "settings.json");
        var store = new JsonAppSettingsStore(settingsPath);
        var expected = Path.GetFullPath(Path.Combine(folder.Path, "STL Library"));

        await store.SaveAsync(new TerrainBuilderSettings { LastLibraryFolder = expected });
        var loaded = await store.LoadAsync();

        Assert.Equal(expected, loaded.LastLibraryFolder);
    }

    [Fact]
    public async Task Load_WithDamagedSettings_ReturnsDefaults()
    {
        using var folder = new TemporaryFolder();
        var settingsPath = Path.Combine(folder.Path, "settings.json");
        await File.WriteAllTextAsync(settingsPath, "{not valid json");

        var loaded = await new JsonAppSettingsStore(settingsPath).LoadAsync();

        Assert.Null(loaded.LastLibraryFolder);
    }
}
