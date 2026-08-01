using TerrainBuilder.Core.Models;
using TerrainBuilder.Core.Services;

namespace TerrainBuilder.Infrastructure.Export;

public sealed class PrintExportService : IPrintExportService
{
    public async Task<PrintExportResult> ExportAsync(
        IEnumerable<PrintListItem> printItems,
        string destinationParentFolder,
        string projectName,
        CancellationToken cancellationToken = default)
    {
        var parentFolder = Path.GetFullPath(destinationParentFolder);
        if (!Directory.Exists(parentFolder)) throw new DirectoryNotFoundException(parentFolder);

        var exportFolder = CreateUniqueExportFolder(parentFolder, projectName);
        Directory.CreateDirectory(exportFolder);
        var missingFiles = new List<string>();
        var filesCopied = 0;

        foreach (var sourcePath in printItems
                     .Select(item => Path.GetFullPath(item.FullPath))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!File.Exists(sourcePath))
            {
                missingFiles.Add(sourcePath);
                continue;
            }

            var destinationPath = CreateUniqueFilePath(exportFolder, Path.GetFileName(sourcePath));
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
            await source.CopyToAsync(destination, cancellationToken);
            filesCopied++;
        }

        return new PrintExportResult(exportFolder, filesCopied, missingFiles);
    }

    private static string CreateUniqueExportFolder(string parentFolder, string projectName)
    {
        var safeProjectName = SanitizeName(projectName);
        var baseName = $"{(string.IsNullOrWhiteSpace(safeProjectName) ? "Terrain Project" : safeProjectName)} - STL Export";
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
}
