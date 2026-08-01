using TerrainBuilder.Core.Models;

namespace TerrainBuilder.Core.Services;

public interface IStlParser
{
    Task<ModelDimensions> ReadDimensionsAsync(string filePath, CancellationToken cancellationToken = default);
    Task<StlMeshData> LoadMeshAsync(string filePath, CancellationToken cancellationToken = default);
}
