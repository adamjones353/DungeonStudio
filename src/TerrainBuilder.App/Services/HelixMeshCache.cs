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
    private const int CacheVersion = 2;
    private const int SimplificationAggressiveness = 5;
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
            ViewportMeshDetailPolicy.MinimumTriangleBudget,
            ViewportMeshDetailPolicy.MaximumTriangleBudget,
            ViewportMeshDetailPolicy.RetainedTriangleRatio,
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

        var sourceTriangleCount = triangleIndices.Count / 3;
        var targetTriangleCount = ViewportMeshDetailPolicy.GetTargetTriangleCount(sourceTriangleCount);
        if (targetTriangleCount < sourceTriangleCount)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sourceStatistics = AnalyzeGeometry(viewportMesh, cancellationToken);
            var simplified = TrySimplify(
                viewportMesh,
                targetTriangleCount,
                sourceStatistics,
                cancellationToken);

            if (simplified is null)
            {
                var retryTarget = ViewportMeshDetailPolicy.GetSafetyRetryTriangleCount(
                    sourceTriangleCount,
                    targetTriangleCount);
                if (retryTarget < sourceTriangleCount)
                {
                    simplified = TrySimplify(
                        viewportMesh,
                        retryTarget,
                        sourceStatistics,
                        cancellationToken);
                }
            }

            if (simplified is not null) viewportMesh = simplified;
        }

        cancellationToken.ThrowIfCancellationRequested();
        viewportMesh.Normals = new Vector3Collection(viewportMesh.CalculateNormals());
        return viewportMesh.ToMeshGeometry3D();
    }

    private static HelixToolkit.Geometry.MeshGeometry3D? TrySimplify(
        HelixToolkit.Geometry.MeshGeometry3D source,
        int targetTriangleCount,
        MeshStatistics sourceStatistics,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var candidate = new MeshSimplification(source).Simplify(
                targetTriangleCount,
                aggressive: SimplificationAggressiveness,
                verbose: false,
                lossless: false);
            var candidateStatistics = AnalyzeGeometry(candidate, cancellationToken);
            return IsAcceptableSimplification(sourceStatistics, candidateStatistics)
                ? candidate
                : null;
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or ArgumentException or IndexOutOfRangeException)
        {
            return null;
        }
    }

    private static MeshStatistics AnalyzeGeometry(
        HelixToolkit.Geometry.MeshGeometry3D geometry,
        CancellationToken cancellationToken)
    {
        var positions = geometry.Positions;
        var indices = geometry.TriangleIndices;
        if (positions is null || indices is null || positions.Count == 0 || indices.Count < 3)
        {
            return MeshStatistics.Invalid;
        }

        var minimum = new Vector3(float.PositiveInfinity);
        var maximum = new Vector3(float.NegativeInfinity);
        foreach (var position in positions)
        {
            if (!float.IsFinite(position.X) || !float.IsFinite(position.Y) || !float.IsFinite(position.Z))
            {
                return MeshStatistics.Invalid;
            }

            minimum = Vector3.Min(minimum, position);
            maximum = Vector3.Max(maximum, position);
        }

        double surfaceArea = 0;
        double maximumEdgeLengthSquared = 0;
        var validTriangles = 0;
        for (var index = 0; index + 2 < indices.Count; index += 3)
        {
            if ((index & 0x3fff) == 0) cancellationToken.ThrowIfCancellationRequested();
            var aIndex = indices[index];
            var bIndex = indices[index + 1];
            var cIndex = indices[index + 2];
            if ((uint)aIndex >= (uint)positions.Count ||
                (uint)bIndex >= (uint)positions.Count ||
                (uint)cIndex >= (uint)positions.Count)
            {
                return MeshStatistics.Invalid;
            }

            var a = positions[aIndex];
            var b = positions[bIndex];
            var c = positions[cIndex];
            var ab = b - a;
            var ac = c - a;
            var bc = c - b;
            var doubledArea = Vector3.Cross(ab, ac).Length();
            if (!float.IsFinite(doubledArea) || doubledArea <= float.Epsilon) continue;

            surfaceArea += doubledArea * 0.5;
            maximumEdgeLengthSquared = Math.Max(
                maximumEdgeLengthSquared,
                Math.Max(ab.LengthSquared(), Math.Max(ac.LengthSquared(), bc.LengthSquared())));
            validTriangles++;
        }

        return new MeshStatistics(
            true,
            minimum,
            maximum,
            surfaceArea,
            maximumEdgeLengthSquared,
            validTriangles);
    }

    private static bool IsAcceptableSimplification(
        MeshStatistics source,
        MeshStatistics candidate)
    {
        if (!source.IsValid || !candidate.IsValid || candidate.ValidTriangleCount == 0) return false;

        var extent = source.Maximum - source.Minimum;
        var tolerance = Math.Max(0.1, Math.Max(extent.X, Math.Max(extent.Y, extent.Z)) * 0.02);
        if (!BoundsMatch(source, candidate, tolerance)) return false;

        var areaRatio = candidate.SurfaceArea / source.SurfaceArea;
        if (!double.IsFinite(areaRatio) || areaRatio < 0.55 || areaRatio > 1.10) return false;

        return candidate.MaximumEdgeLengthSquared <=
               Math.Max(source.MaximumEdgeLengthSquared * 9, tolerance * tolerance);
    }

    private static bool BoundsMatch(
        MeshStatistics source,
        MeshStatistics candidate,
        double tolerance) =>
        Math.Abs(source.Minimum.X - candidate.Minimum.X) <= tolerance &&
        Math.Abs(source.Minimum.Y - candidate.Minimum.Y) <= tolerance &&
        Math.Abs(source.Minimum.Z - candidate.Minimum.Z) <= tolerance &&
        Math.Abs(source.Maximum.X - candidate.Maximum.X) <= tolerance &&
        Math.Abs(source.Maximum.Y - candidate.Maximum.Y) <= tolerance &&
        Math.Abs(source.Maximum.Z - candidate.Maximum.Z) <= tolerance;

    private readonly record struct MeshStatistics(
        bool IsValid,
        Vector3 Minimum,
        Vector3 Maximum,
        double SurfaceArea,
        double MaximumEdgeLengthSquared,
        int ValidTriangleCount)
    {
        public static MeshStatistics Invalid => new(
            false,
            Vector3.Zero,
            Vector3.Zero,
            0,
            0,
            0);
    }
}


