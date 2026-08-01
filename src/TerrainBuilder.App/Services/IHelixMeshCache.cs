using HelixToolkit.SharpDX;

namespace TerrainBuilder.App.Services;

public interface IHelixMeshCache
{
    Task<MeshGeometry3D> GetAsync(string stlPath, CancellationToken cancellationToken = default);
}
