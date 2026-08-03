using System.Globalization;
using System.Text;
using TerrainBuilder.Core.Models;
using TerrainBuilder.Core.Services;
using TerrainBuilder.Infrastructure.Stl;

namespace TerrainBuilder.Infrastructure.Export;

public sealed class PrintExportService : IPrintExportService
{
    private const string PrintListFileName = "Print List.txt";
    private readonly ThreeMfPackageWriter _threeMfWriter;

    public PrintExportService(IStlParser stlParser)
    {
        _threeMfWriter = new ThreeMfPackageWriter(stlParser);
    }

    public PrintExportService()
        : this(new StlParser())
    {
    }

    public async Task<PrintExportResult> ExportAsync(
        IEnumerable<PrintListItem> printItems,
        string destinationParentFolder,
        string projectName,
        CancellationToken cancellationToken = default)
    {
        var parentFolder = Path.GetFullPath(destinationParentFolder);
        if (!Directory.Exists(parentFolder)) throw new DirectoryNotFoundException(parentFolder);

        var items = CombineItemsBySourcePath(printItems);
        var exportFolder = CreateUniqueExportFolder(parentFolder, projectName);
        Directory.CreateDirectory(exportFolder);
        var missingFiles = new List<string>();
        var manifestEntries = new List<ManifestEntry>(items.Count);
        var filesCopied = 0;

        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sourcePath = item.FullPath;
            if (!File.Exists(sourcePath))
            {
                missingFiles.Add(sourcePath);
                manifestEntries.Add(new ManifestEntry(item, null));
                continue;
            }

            var destinationPath = CreateUniqueFilePath(exportFolder, Path.GetFileName(sourcePath));
            await CopyFileAsync(sourcePath, destinationPath, cancellationToken).ConfigureAwait(false);
            manifestEntries.Add(new ManifestEntry(item, Path.GetFileName(destinationPath)));
            filesCopied++;
        }

        var availableItems = items.Where(item => File.Exists(item.FullPath)).ToArray();
        var threeMfResult = await _threeMfWriter.WriteAsync(
            exportFolder,
            projectName,
            availableItems,
            cancellationToken).ConfigureAwait(false);

        await WritePrintListAsync(
            Path.Combine(exportFolder, PrintListFileName),
            projectName,
            manifestEntries,
            threeMfResult,
            cancellationToken).ConfigureAwait(false);

        return new PrintExportResult(exportFolder, filesCopied, missingFiles)
        {
            ThreeMfFilePath = threeMfResult.FilePath,
            ThreeMfPlateFilePaths = threeMfResult.PlateFilePaths,
            ThreeMfOmittedFiles = threeMfResult.OmittedFiles,
            ThreeMfPlateCount = threeMfResult.Plan.Plates.Count,
            OversizePlacementCount = threeMfResult.Plan.OversizePlacements.Count
        };
    }

    private static IReadOnlyList<PrintListItem> CombineItemsBySourcePath(IEnumerable<PrintListItem> printItems) =>
        printItems
            .Select(item => item with { FullPath = Path.GetFullPath(item.FullPath) })
            .GroupBy(item => item.FullPath, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var first = group.First();
                return first with
                {
                    FullPath = group.Key,
                    Quantity = group.Sum(item => Math.Max(0, item.Quantity))
                };
            })
            .OrderBy(item => item.ModelName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.FullPath, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static async Task CopyFileAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        await using var source = new FileStream(
            sourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            128 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using var destination = new FileStream(
            destinationPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            128 * 1024,
            FileOptions.Asynchronous);
        await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
    }

    private static Task WritePrintListAsync(
        string filePath,
        string projectName,
        IReadOnlyList<ManifestEntry> entries,
        ThreeMfPackageResult threeMfResult,
        CancellationToken cancellationToken)
    {
        var omittedFromThreeMf = threeMfResult.OmittedFiles.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var text = new StringBuilder();
        text.AppendLine("DungeonStudio Print List");
        text.AppendLine($"Project: {projectName}");
        text.AppendLine($"Total pieces to print: {entries.Sum(entry => entry.Item.Quantity):N0}");
        text.AppendLine($"Unique models: {entries.Count:N0}");
        text.AppendLine("Printer plate preset: Creality Hi");
        text.AppendLine($"Build volume: {PrintPlatePlanner.BedWidthMm:0} x {PrintPlatePlanner.BedDepthMm:0} x {PrintPlatePlanner.BedHeightMm:0} mm");
        text.AppendLine($"Automatic layout area: {PrintPlatePlanner.UsableWidth:0} x {PrintPlatePlanner.UsableDepth:0} mm (5 mm edge margin)");
        text.AppendLine($"Plates required: {threeMfResult.Plan.Plates.Count:N0}");
        text.AppendLine(threeMfResult.FilePath is null
            ? "Multi-plate 3MF project: NOT CREATED - NO VALID STL MESHES"
            : $"Multi-plate 3MF project: {Path.GetFileName(threeMfResult.FilePath)}");
        text.AppendLine(threeMfResult.PlateFilePaths.Count == 0
            ? "Portable plate files: NOT CREATED"
            : $"Portable plate files: {ThreeMfPackageWriter.PlateFolderName} ({threeMfResult.PlateFilePaths.Count:N0} file(s))");
        if (threeMfResult.Plan.OversizePlacements.Count > 0)
        {
            text.AppendLine($"WARNING: {threeMfResult.Plan.OversizePlacements.Count:N0} piece(s) exceed the safe plate area or 300 mm height; verify them in the slicer.");
        }
        text.AppendLine();

        for (var index = 0; index < entries.Count; index++)
        {
            var entry = entries[index];
            var item = entry.Item;
            text.AppendLine($"{index + 1}. {item.ModelName}");
            text.AppendLine($"   QUANTITY TO PRINT: {item.Quantity:N0}");
            text.AppendLine($"   Dimensions: {FormatDimensions(item.Dimensions)}");
            text.AppendLine($"   Plates: {FormatPlateAllocation(item, threeMfResult)}");
            text.AppendLine($"   Exported STL: {entry.ExportedFileName ?? "NOT COPIED - SOURCE FILE MISSING"}");
            text.AppendLine($"   3MF package: {FormatThreeMfStatus(item, entry, threeMfResult, omittedFromThreeMf)}");
            text.AppendLine($"   Source folder: {item.SourceFolder}");
            text.AppendLine($"   Source STL: {item.FullPath}");
            text.AppendLine();
        }

        return File.WriteAllTextAsync(
            filePath,
            text.ToString(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            cancellationToken);
    }

    private static string FormatPlateAllocation(PrintListItem item, ThreeMfPackageResult result)
    {
        var allocations = result.Plan.Plates
            .Select(plate => new
            {
                plate.Number,
                Count = plate.Placements.Count(placement =>
                    string.Equals(placement.Item.FullPath, item.FullPath, StringComparison.OrdinalIgnoreCase))
            })
            .Where(allocation => allocation.Count > 0)
            .Select(allocation => $"Plate {allocation.Number:00}: {allocation.Count:N0}")
            .ToArray();
        return allocations.Length == 0 ? "Not allocated" : string.Join(", ", allocations);
    }

    private static string FormatThreeMfStatus(
        PrintListItem item,
        ManifestEntry entry,
        ThreeMfPackageResult result,
        IReadOnlySet<string> omittedFiles)
    {
        if (entry.ExportedFileName is null) return "NOT INCLUDED - SOURCE FILE MISSING";
        if (omittedFiles.Contains(item.FullPath)) return "NOT INCLUDED - STL COULD NOT BE READ";
        return result.FilePath is null
            ? "NOT INCLUDED"
            : $"Included as {item.Quantity:N0} build item(s)";
    }

    private static string FormatDimensions(ModelDimensions dimensions) => string.Create(
        CultureInfo.InvariantCulture,
        $"{dimensions.WidthMm:0.#} x {dimensions.DepthMm:0.#} x {dimensions.HeightMm:0.#} mm");

    private static string CreateUniqueExportFolder(string parentFolder, string projectName)
    {
        var safeProjectName = SanitizeName(projectName);
        var baseName = $"{(string.IsNullOrWhiteSpace(safeProjectName) ? "Terrain Project" : safeProjectName)} - Print Export";
        var candidate = Path.Combine(parentFolder, baseName);
        for (var suffix = 2; Directory.Exists(candidate); suffix++)
        {
            candidate = Path.Combine(parentFolder, $"{baseName} ({suffix})");
        }

        return candidate;
    }

    private static string CreateUniqueFilePath(string folder, string fileName)
    {
        var safeName = SanitizeName(Path.GetFileNameWithoutExtension(fileName));
        var extension = Path.GetExtension(fileName);
        var candidate = Path.Combine(folder, safeName + extension);
        for (var suffix = 2; File.Exists(candidate); suffix++)
        {
            candidate = Path.Combine(folder, $"{safeName} ({suffix}){extension}");
        }

        return candidate;
    }

    private static string SanitizeName(string value) => string.Concat(value.Select(character =>
        Path.GetInvalidFileNameChars().Contains(character) ? '_' : character)).Trim();

    private sealed record ManifestEntry(PrintListItem Item, string? ExportedFileName);
}
