using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;
using TerrainBuilder.Core.Models;
using TerrainBuilder.Infrastructure.Export;

namespace TerrainBuilder.Tests;

public sealed class ThreeMfPrintExportTests
{
    private static readonly XNamespace CoreNamespace =
        "http://schemas.microsoft.com/3dmanufacturing/core/2015/02";

    [Fact]
    public async Task Export_CreatesSelfContainedThreeMfWithOneBuildItemPerRequiredCopy()
    {
        using var folder = new TemporaryFolder();
        var sourceFolder = Directory.CreateDirectory(Path.Combine(folder.Path, "models")).FullName;
        var outputFolder = Directory.CreateDirectory(Path.Combine(folder.Path, "output")).FullName;
        var wallPath = Path.Combine(sourceFolder, "wall.stl");
        var floorPath = Path.Combine(sourceFolder, "floor.stl");
        await WriteTetrahedronAsync(wallPath, 20);
        await WriteTetrahedronAsync(floorPath, 10);

        var result = await new PrintExportService().ExportAsync(
            [Item(wallPath, "Wall", 3), Item(floorPath, "Floor", 2)],
            outputFolder,
            "Five Piece Room");

        Assert.NotNull(result.ThreeMfFilePath);
        Assert.True(File.Exists(result.ThreeMfFilePath));
        Assert.Single(result.ThreeMfPlateFilePaths);
        Assert.True(File.Exists(result.ThreeMfPlateFilePaths[0]));
        Assert.Equal(1, result.ThreeMfPlateCount);
        Assert.Empty(result.ThreeMfOmittedFiles);

        using var archive = ZipFile.OpenRead(result.ThreeMfFilePath);
        Assert.NotNull(archive.GetEntry("[Content_Types].xml"));
        Assert.NotNull(archive.GetEntry("_rels/.rels"));
        Assert.NotNull(archive.GetEntry("Metadata/model_settings.config"));
        var modelEntry = Assert.IsType<ZipArchiveEntry>(archive.GetEntry("3D/3dmodel.model"));
        await using var modelStream = modelEntry.Open();
        var document = await XDocument.LoadAsync(modelStream, LoadOptions.None, CancellationToken.None);

        var model = Assert.IsType<XElement>(document.Root);
        Assert.Equal("millimeter", model.Attribute("unit")?.Value);
        var objects = model
            .Element(CoreNamespace + "resources")!
            .Elements(CoreNamespace + "object")
            .ToArray();
        Assert.Equal(2, objects.Length);
        Assert.All(objects, element =>
        {
            Assert.Equal(4, element.Descendants(CoreNamespace + "vertex").Count());
            Assert.Equal(4, element.Descendants(CoreNamespace + "triangle").Count());
        });

        var buildItems = model
            .Element(CoreNamespace + "build")!
            .Elements(CoreNamespace + "item")
            .ToArray();
        Assert.Equal(5, buildItems.Length);
        var quantities = buildItems
            .GroupBy(element => element.Attribute("objectid")!.Value)
            .Select(group => group.Count())
            .Order()
            .ToArray();
        Assert.Equal([2, 3], quantities);
        Assert.All(buildItems, AssertValidPositiveTransform);

        var printList = await File.ReadAllTextAsync(Path.Combine(result.ExportFolder, "Print List.txt"));
        Assert.Contains("Printer plate preset: Creality Hi", printList);
        Assert.Contains("Build volume: 260 x 260 x 300 mm", printList);
        Assert.Contains("Plates required: 1", printList);
        Assert.Contains("Multi-plate 3MF project: Print Package.project.3mf", printList);
        Assert.Contains("Plate 01:", printList);
        Assert.Contains("Included as 3 build item(s)", printList);
        Assert.Contains("Included as 2 build item(s)", printList);
    }

    [Fact]
    public async Task Export_CreatesNumberedPortableFilesWhenLayoutNeedsMultiplePlates()
    {
        using var folder = new TemporaryFolder();
        var sourceFolder = Directory.CreateDirectory(Path.Combine(folder.Path, "models")).FullName;
        var outputFolder = Directory.CreateDirectory(Path.Combine(folder.Path, "output")).FullName;
        var modelPath = Path.Combine(sourceFolder, "large-floor.stl");
        await WriteTetrahedronAsync(modelPath, 130);

        var result = await new PrintExportService().ExportAsync(
            [Item(modelPath, "Large Floor", 3, 130)],
            outputFolder,
            "Large Room");

        Assert.Equal(3, result.ThreeMfPlateCount);
        Assert.Equal(3, result.ThreeMfPlateFilePaths.Count);
        Assert.Equal(
            ["Plate 01.build.3mf", "Plate 02.build.3mf", "Plate 03.build.3mf"],
            result.ThreeMfPlateFilePaths.Select(Path.GetFileName));

        using (var projectArchive = ZipFile.OpenRead(result.ThreeMfFilePath!))
        {
            var settingsEntry = Assert.IsType<ZipArchiveEntry>(
                projectArchive.GetEntry("Metadata/model_settings.config"));
            await using var settingsStream = settingsEntry.Open();
            var settings = await XDocument.LoadAsync(
                settingsStream,
                LoadOptions.None,
                CancellationToken.None);
            var plates = settings.Root!.Elements("plate").ToArray();
            Assert.Equal(3, plates.Length);
            Assert.All(plates, plate => Assert.Single(plate.Elements("model_instance")));
        }

        foreach (var platePath in result.ThreeMfPlateFilePaths)
        {
            using var archive = ZipFile.OpenRead(platePath);
            var modelEntry = Assert.IsType<ZipArchiveEntry>(archive.GetEntry("3D/3dmodel.model"));
            await using var stream = modelEntry.Open();
            var document = await XDocument.LoadAsync(stream, LoadOptions.None, CancellationToken.None);
            Assert.Single(document.Root!.Element(CoreNamespace + "build")!.Elements(CoreNamespace + "item"));
        }

        var printList = await File.ReadAllTextAsync(Path.Combine(result.ExportFolder, "Print List.txt"));
        Assert.Contains("Plates required: 3", printList);
        Assert.Contains("Plate 01: 1, Plate 02: 1, Plate 03: 1", printList);
    }

    private static void AssertValidPositiveTransform(XElement item)
    {
        var values = item.Attribute("transform")!.Value
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(value => double.Parse(value, CultureInfo.InvariantCulture))
            .ToArray();
        Assert.Equal(12, values.Length);
        Assert.True(values[9] >= 0);
        Assert.True(values[10] >= 0);
        Assert.True(values[11] >= 0);
    }

    private static PrintListItem Item(string path, string name, int quantity, double size = 20) => new()
    {
        ModelName = name,
        FullPath = path,
        SourceFolder = Path.GetDirectoryName(path)!,
        Dimensions = new ModelDimensions(size, size, size),
        Quantity = quantity
    };

    private static Task WriteTetrahedronAsync(string path, double size)
    {
        var s = size.ToString(CultureInfo.InvariantCulture);
        var contents = $"""
                       solid tetrahedron
                         facet normal 0 0 -1
                           outer loop
                             vertex 0 0 0
                             vertex 0 {s} 0
                             vertex {s} 0 0
                           endloop
                         endfacet
                         facet normal 0 -1 0
                           outer loop
                             vertex 0 0 0
                             vertex {s} 0 0
                             vertex 0 0 {s}
                           endloop
                         endfacet
                         facet normal -1 0 0
                           outer loop
                             vertex 0 0 0
                             vertex 0 0 {s}
                             vertex 0 {s} 0
                           endloop
                         endfacet
                         facet normal 1 1 1
                           outer loop
                             vertex {s} 0 0
                             vertex 0 {s} 0
                             vertex 0 0 {s}
                           endloop
                         endfacet
                       endsolid tetrahedron
                       """;
        return File.WriteAllTextAsync(path, contents);
    }
}




