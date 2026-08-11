using TerrainBuilder.Core.Models;

namespace TerrainBuilder.Core.Services;

public interface ILibraryIndexService
{
    Task<IReadOnlyList<ModelLibraryItem>> ScanAsync(
        string rootFolder,
        IProgress<LibraryScanProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
