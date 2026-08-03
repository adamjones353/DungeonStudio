using System.Diagnostics;
using TerrainBuilder.App.Services;
using TerrainBuilder.Core.Models;
using TerrainBuilder.Infrastructure.Export;
using TerrainBuilder.Infrastructure.Stl;

if (args.Length == 3 && args[0].Equals("--3mf", StringComparison.OrdinalIgnoreCase))
{
    var sourcePath = Path.GetFullPath(args[1]);
    var outputFolder = Path.GetFullPath(args[2]);
    Directory.CreateDirectory(outputFolder);
    var parser = new StlParser();
    var dimensions = await parser.ReadDimensionsAsync(sourcePath);
    var item = new PrintListItem
    {
        ModelName = Path.GetFileNameWithoutExtension(sourcePath),
        FullPath = sourcePath,
        SourceFolder = Path.GetDirectoryName(sourcePath)!,
        Dimensions = dimensions,
        Quantity = 2
    };
    var result = await new PrintExportService(parser).ExportAsync([item], outputFolder, "3MF Probe");
    Console.WriteLine($"Package={result.ThreeMfFilePath}");
    Console.WriteLine($"Copied={result.FilesCopied}; Omitted={result.ThreeMfOmittedFiles.Count}");
    return;
}

if (args.Length != 1) throw new ArgumentException("Supply one STL path, or --3mf <STL path> <output folder>.");
var cache = new HelixMeshCache(new StlParser());
var stopwatch = Stopwatch.StartNew();
var mesh = await cache.GetAsync(Path.GetFullPath(args[0]));
stopwatch.Stop();
Console.WriteLine($"Triangles={mesh.Indices?.Count / 3:N0}; Vertices={mesh.Positions?.Count:N0}; Elapsed={stopwatch.Elapsed}");
