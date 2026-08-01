namespace TerrainBuilder.Core.Models;

public sealed record ModelLibraryItem
{
    public required string FileName { get; init; }
    public required string FullPath { get; init; }
    public required string FolderPath { get; init; }
    public long FileSizeBytes { get; init; }
    public ModelDimensions Dimensions { get; init; }
    public DateTime LastModifiedUtc { get; init; }
    public string? ThumbnailPath { get; init; }
    public bool IsValid { get; init; } = true;
    public string? ErrorMessage { get; init; }

    public string DisplayName => Path.GetFileNameWithoutExtension(FileName);
    public string DimensionsDisplay => IsValid ? Dimensions.ToDisplayString() : "Invalid STL";
}
