namespace TerrainBuilder.Core.Models;

public sealed record PrintExportResult(
    string ExportFolder,
    int FilesCopied,
    IReadOnlyList<string> MissingFiles)
{
    /// <summary>The Creality-compatible multi-plate project package.</summary>
    public string? ThreeMfFilePath { get; init; }

    /// <summary>Portable one-build-tray 3MF files, one for each planned plate.</summary>
    public IReadOnlyList<string> ThreeMfPlateFilePaths { get; init; } = [];

    public IReadOnlyList<string> ThreeMfOmittedFiles { get; init; } = [];
    public int ThreeMfPlateCount { get; init; }
    public int OversizePlacementCount { get; init; }
}
