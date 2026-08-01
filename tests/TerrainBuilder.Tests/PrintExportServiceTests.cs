using TerrainBuilder.Core.Models;
using TerrainBuilder.Infrastructure.Export;

namespace TerrainBuilder.Tests;

public sealed class PrintExportServiceTests
{
    [Fact]
    public async Task Export_CopiesUniqueSourcesWithoutOverwritingNameCollisions()
    {
        using var folder = new TemporaryFolder();
        var firstFolder = Directory.CreateDirectory(Path.Combine(folder.Path, "first")).FullName;
        var secondFolder = Directory.CreateDirectory(Path.Combine(folder.Path, "second")).FullName;
        var outputFolder = Directory.CreateDirectory(Path.Combine(folder.Path, "output")).FullName;
        var first = Path.Combine(firstFolder, "wall.stl");
        var second = Path.Combine(secondFolder, "wall.stl");
        await File.WriteAllTextAsync(first, "first mesh");
        await File.WriteAllTextAsync(second, "second mesh");

        var result = await new PrintExportService().ExportAsync(
            [Item(first), Item(first), Item(second)],
            outputFolder,
            "Room One");

        Assert.Equal(2, result.FilesCopied);
        Assert.Empty(result.MissingFiles);
        Assert.True(File.Exists(Path.Combine(result.ExportFolder, "wall.stl")));
        Assert.True(File.Exists(Path.Combine(result.ExportFolder, "wall (2).stl")));
    }

    [Fact]
    public async Task Export_ReportsMissingFilesAndCreatesANewFolderForEachRun()
    {
        using var folder = new TemporaryFolder();
        var outputFolder = Directory.CreateDirectory(Path.Combine(folder.Path, "output")).FullName;
        var missing = Path.Combine(folder.Path, "missing.stl");
        var service = new PrintExportService();

        var first = await service.ExportAsync([Item(missing)], outputFolder, "Room");
        var second = await service.ExportAsync([Item(missing)], outputFolder, "Room");

        Assert.Single(first.MissingFiles);
        Assert.NotEqual(first.ExportFolder, second.ExportFolder);
        Assert.True(Directory.Exists(first.ExportFolder));
        Assert.True(Directory.Exists(second.ExportFolder));
    }

    private static PrintListItem Item(string path) => new()
    {
        ModelName = Path.GetFileName(path),
        FullPath = path,
        SourceFolder = Path.GetDirectoryName(path)!,
        Dimensions = ModelDimensions.Empty,
        Quantity = 1
    };
}
