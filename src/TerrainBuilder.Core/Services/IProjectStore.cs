using TerrainBuilder.Core.Models;

namespace TerrainBuilder.Core.Services;

public interface IProjectStore
{
    Task SaveAsync(string filePath, TerrainProject project, CancellationToken cancellationToken = default);
    Task<TerrainProject> LoadAsync(string filePath, CancellationToken cancellationToken = default);
}
