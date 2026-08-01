using System.Collections.Concurrent;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using HelixToolkit;
using HelixToolkit.Geometry;
using HelixToolkit.SharpDX;
using TerrainBuilder.Core.Services;

namespace TerrainBuilder.App.Services;

public sealed class HelixMeshCache : IHelixMeshCache
{
    private const int ViewportTriangleLimit = 20_000;
    private const int CacheVersion = 1;
    private const uint CacheMagic = 0x444C4254; // TBLD
    private const int MaximumCachedVertices = 2_000_000;
    private const int MaximumCachedIndices = 6_000_000;

    private readonly IStlParser _parser;
    private readonly string _cacheFolder;
    private readonly ConcurrentDictionary<string, Task<MeshGeometry3D>> _memoryCache =
        new(StringComparer.OrdinalIgnoreCase);

    public HelixMeshCache(IStlParser parser)
    {
        _parser = parser;
        _cacheFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TerrainBuilder",
            "ViewportMeshes");
    }

    public Task<MeshGeometry3D> GetAsync(string stlPath, CancellationToken cancellationToken = default) =>
        _memoryCache.GetOrAdd(stlPath, _ => LoadAsync(stlPath, cancellationToken));

    private async Task<MeshGeometry3D> LoadAsync(string stlPath, CancellationToken cancellationToken)
    {
        var fileInfo = new FileInfo(stlPath);
        var cachePath = GetCachePath(fileInfo);

        try
        {
            if (File.Exists(cachePath))
            {
                return await Task.Run(
                    () => LoadCachedMesh(cachePath, cancellationToken),
                    cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (
            exception is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            // A stale or damaged cache entry is disposable; rebuild it from the source STL.
        }

        var source = await _parser.LoadMeshAsync(stlPath, cancellationToken).ConfigureAwait(false);
        var viewportMesh = await Task.Run(
            () => CreateViewportMesh(source.Positions, source.Indices, cancellationToken),
            cancellationToken).ConfigureAwait(false);

        try
        {
            await Task.Run(
                () => SaveCachedMesh(cachePath, viewportMesh, cancellationToken),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A read-only or temporarily unavailable cache must not prevent model placement.
        }

        return viewportMesh;
    }

    private string GetCachePath(FileInfo fileInfo)
    {
        var keyText = string.Join(
            '|',
            fileInfo.FullName.ToUpperInvariant(),
            fileInfo.Length,
            fileInfo.LastWriteTimeUtc.Ticks,
            ViewportTriangleLimit,
            CacheVersion);
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(keyText)));
        return Path.Combine(_cacheFolder, $"{key}.tbmesh");
    }

    private static MeshGeometry3D LoadCachedMesh(string cachePath, CancellationToken cancellationToken)
    {
        using var stream = new FileStream(
            cachePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            FileOptions.SequentialScan);
        using var reader = new BinaryReader(stream);

        if (reader.ReadUInt32() != CacheMagic || reader.ReadInt32() != CacheVersion)
        {
            throw new InvalidDataException("Unsupported viewport mesh cache format.");
        }

        var positionCount = reader.ReadInt32();
        var indexCount = reader.ReadInt32();
        if (positionCount <= 0 || positionCount > MaximumCachedVertices ||
            indexCount <= 0 || indexCount > MaximumCachedIndices || indexCount % 3 != 0)
        {
            throw new InvalidDataException("Invalid viewport mesh cache dimensions.");
        }

        var positions = new Vector3Collection(positionCount);
        for (var index = 0; index < positionCount; index++)
        {
            if ((index & 0x3fff) == 0) cancellationToken.ThrowIfCancellationRequested();
            positions.Add(new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()));
        }

        var normals = new Vector3Collection(positionCount);
        for (var index = 0; index < positionCount; index++)
        {
            if ((index & 0x3fff) == 0) cancellationToken.ThrowIfCancellationRequested();
            normals.Add(new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()));
        }

        var indices = new IntCollection(indexCount);
        for (var index = 0; index < indexCount; index++)
        {
            if ((index & 0x3fff) == 0) cancellationToken.ThrowIfCancellationRequested();
            var vertexIndex = reader.ReadInt32();
            if ((uint)vertexIndex >= (uint)positionCount)
            {
                throw new InvalidDataException("Viewport mesh cache contains an invalid index.");
            }

            indices.Add(vertexIndex);
        }

        return new MeshGeometry3D
        {
            Positions = positions,
            Normals = normals,
            Indices = indices
        };
    }

    private static void SaveCachedMesh(
        string cachePath,
        MeshGeometry3D mesh,
        CancellationToken cancellationToken)
    {
        var positions = mesh.Positions ?? throw new InvalidDataException("Viewport mesh has no positions.");
        var normals = mesh.Normals ?? throw new InvalidDataException("Viewport mesh has no normals.");
        var indices = mesh.Indices ?? throw new InvalidDataException("Viewport mesh has no indices.");
        if (positions.Count != normals.Count)
        {
            throw new InvalidDataException("Viewport mesh position and normal counts differ.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
        var temporaryPath = $"{cachePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            using (var stream = new FileStream(
                       temporaryPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None,
                       bufferSize: 64 * 1024,
                       FileOptions.SequentialScan))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(CacheMagic);
                writer.Write(CacheVersion);
                writer.Write(positions.Count);
                writer.Write(indices.Count);

                foreach (var position in positions)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    writer.Write(position.X);
                    writer.Write(position.Y);
                    writer.Write(position.Z);
                }

                foreach (var normal in normals)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    writer.Write(normal.X);
                    writer.Write(normal.Y);
                    writer.Write(normal.Z);
                }

                foreach (var index in indices)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    writer.Write(index);
                }
            }

            File.Move(temporaryPath, cachePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static MeshGeometry3D CreateViewportMesh(
        IReadOnlyList<Vector3> sourcePositions,
        IReadOnlyList<int> sourceIndices,
        CancellationToken cancellationToken)
    {
        var positions = new Vector3Collection();
        var triangleIndices = new IntCollection(sourceIndices.Count);
        var vertexLookup = new Dictionary<Vector3, int>();

        int GetVertexIndex(Vector3 vertex)
        {
            if (vertexLookup.TryGetValue(vertex, out var existing)) return existing;
            var index = positions.Count;
            positions.Add(vertex);
            vertexLookup.Add(vertex, index);
            return index;
        }

        for (var triangle = 0; triangle + 2 < sourceIndices.Count; triangle += 3)
        {
            if ((triangle & 0x3fff) == 0) cancellationToken.ThrowIfCancellationRequested();
            var a = GetVertexIndex(sourcePositions[sourceIndices[triangle]]);
            var b = GetVertexIndex(sourcePositions[sourceIndices[triangle + 1]]);
            var c = GetVertexIndex(sourcePositions[sourceIndices[triangle + 2]]);
            if (a == b || b == c || c == a) continue;
            triangleIndices.Add(a);
            triangleIndices.Add(b);
            triangleIndices.Add(c);
        }

        var viewportMesh = new HelixToolkit.Geometry.MeshGeometry3D
        {
            Positions = positions,
            TriangleIndices = triangleIndices
        };

        if (triangleIndices.Count / 3 > ViewportTriangleLimit)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var simplifier = new MeshSimplification(viewportMesh);
            viewportMesh = simplifier.Simplify(
                ViewportTriangleLimit,
                aggressive: 7,
                verbose: false,
                lossless: false);
        }

        cancellationToken.ThrowIfCancellationRequested();
        viewportMesh.Normals = new Vector3Collection(viewportMesh.CalculateNormals());
        return viewportMesh.ToMeshGeometry3D();
    }
}
