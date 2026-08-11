namespace TerrainBuilder.Core.Models;

public sealed record LibraryScanProgress(
    int Percentage,
    int CompletedFiles,
    int TotalFiles,
    string? CurrentFilePath,
    IReadOnlyList<ModelLibraryItem>? CompletedItems = null);
