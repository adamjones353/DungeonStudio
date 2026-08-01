namespace TerrainBuilder.Core.Models;

public sealed record PrintExportResult(
    string ExportFolder,
    int FilesCopied,
    IReadOnlyList<string> MissingFiles);
