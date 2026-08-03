using TerrainBuilder.Core.Models;
using TerrainBuilder.Infrastructure.Export;

namespace TerrainBuilder.Tests;

public sealed class PrintExportManifestTests
{
    [Fact]
    public async Task Export_WritesRequiredQuantitiesToPlainTextPrintList()
    {
        using var folder = new TemporaryFolder();
        var sourceFolder = Directory.CreateDirectory(Path.Combine(folder.Path, "models")).FullName;
        var outputFolder = Directory.CreateDirectory(Path.Combine(folder.Path, "output")).FullName;
        var wallPath = Path.Combine(sourceFolder, "stone wall.stl");
        var floorPath = Path.Combine(sourceFolder, "wood floor.stl");
        await File.WriteAllTextAsync(wallPath, "wall mesh");
        await File.WriteAllTextAsync(floorPath, "floor mesh");

        var result = await new PrintExportService().ExportAsync(
            [
                Item(wallPath, "Stone wall", 4, new ModelDimensions(50.8, 12.7, 50.8)),
                Item(floorPath, "Wood floor", 2, new ModelDimensions(50.8, 50.8, 3.2))
            ],
            outputFolder,
            "Room One");

        var printListPath = Path.Combine(result.ExportFolder, "Print List.txt");
        Assert.True(File.Exists(printListPath));
        var text = await File.ReadAllTextAsync(printListPath);
        Assert.Contains("Total pieces to print: 6", text);
        Assert.Contains("Stone wall", text);
        Assert.Contains("QUANTITY TO PRINT: 4", text);
        Assert.Contains("Wood floor", text);
        Assert.Contains("QUANTITY TO PRINT: 2", text);
        Assert.Contains("50.8 x 12.7 x 50.8 mm", text);
        Assert.Contains("Exported STL: stone wall.stl", text);
    }

    [Fact]
    public async Task Export_CombinesDuplicateEntriesAndMarksMissingSources()
    {
        using var folder = new TemporaryFolder();
        var outputFolder = Directory.CreateDirectory(Path.Combine(folder.Path, "output")).FullName;
        var missingPath = Path.Combine(folder.Path, "missing wall.stl");

        var result = await new PrintExportService().ExportAsync(
            [Item(missingPath, "Missing wall", 2), Item(missingPath, "Missing wall", 3)],
            outputFolder,
            "Missing Test");

        var text = await File.ReadAllTextAsync(Path.Combine(result.ExportFolder, "Print List.txt"));
        Assert.Contains("Total pieces to print: 5", text);
        Assert.Contains("Unique models: 1", text);
        Assert.Contains("QUANTITY TO PRINT: 5", text);
        Assert.Contains("NOT COPIED - SOURCE FILE MISSING", text);
        Assert.Single(result.MissingFiles);
    }

    private static PrintListItem Item(
        string path,
        string name,
        int quantity,
        ModelDimensions? dimensions = null) => new()
    {
        ModelName = name,
        FullPath = path,
        SourceFolder = Path.GetDirectoryName(path)!,
        Dimensions = dimensions ?? ModelDimensions.Empty,
        Quantity = quantity
    };
}
