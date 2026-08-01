using TerrainBuilder.Core.Models;

namespace TerrainBuilder.Core.Services;

public interface IAppSettingsStore
{
    Task<TerrainBuilderSettings> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(TerrainBuilderSettings settings, CancellationToken cancellationToken = default);
}
