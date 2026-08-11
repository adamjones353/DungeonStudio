using System.Buffers.Binary;
using System.Globalization;
using System.Numerics;
using System.Text;
using TerrainBuilder.Core.Models;
using TerrainBuilder.Core.Services;

namespace TerrainBuilder.Infrastructure.Stl;

public sealed class StlParser : IStlParser
{
    private const int BinaryHeaderSize = 84;
    private const int BinaryTriangleSize = 50;
    private const int DimensionReadBatchTriangles = 2500;

    public async Task<ModelDimensions> ReadDimensionsAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ValidatePath(filePath);
        await using var stream = OpenRead(filePath);
        return await IsBinaryAsync(stream, cancellationToken)
            ? await ReadBinaryDimensionsAsync(stream, cancellationToken)
            : await ReadAsciiDimensionsAsync(stream, cancellationToken);
    }

    public async Task<StlMeshData> LoadMeshAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ValidatePath(filePath);
        await using var stream = OpenRead(filePath);
        return await IsBinaryAsync(stream, cancellationToken)
            ? await ReadBinaryMeshAsync(stream, cancellationToken)
            : await ReadAsciiMeshAsync(stream, cancellationToken);
    }

    private static FileStream OpenRead(string filePath) => new(
        filePath,
        FileMode.Open,
        FileAccess.Read,
        FileShare.Read,
        bufferSize: 128 * 1024,
        FileOptions.Asynchronous | FileOptions.SequentialScan);

    private static void ValidatePath(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !Path.IsPathFullyQualified(filePath))
        {
            throw new ArgumentException("An absolute STL path is required.", nameof(filePath));
        }

        if (!string.Equals(Path.GetExtension(filePath), ".stl", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException("Only STL files are supported in Phase 1.");
        }
    }

    private static async Task<bool> IsBinaryAsync(Stream stream, CancellationToken cancellationToken)
    {
        if (stream.Length < BinaryHeaderSize)
        {
            stream.Position = 0;
            return false;
        }

        var header = new byte[BinaryHeaderSize];
        await stream.ReadExactlyAsync(header, cancellationToken);
        stream.Position = 0;

        var triangleCount = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(80, 4));
        var expectedLength = BinaryHeaderSize + (long)triangleCount * BinaryTriangleSize;
        var beginsWithSolid = Encoding.ASCII.GetString(header, 0, 5)
            .Equals("solid", StringComparison.OrdinalIgnoreCase);

        return expectedLength == stream.Length ||
               (!beginsWithSolid && triangleCount > 0 && expectedLength <= stream.Length);
    }

    private static async Task<ModelDimensions> ReadBinaryDimensionsAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var header = new byte[BinaryHeaderSize];
        await stream.ReadExactlyAsync(header, cancellationToken);
        var triangleCount = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(80, 4));
        ValidateTriangleCount(triangleCount, stream.Length);

        var bounds = BoundsBuilder.Empty;
        var triangles = new byte[BinaryTriangleSize * DimensionReadBatchTriangles];
        var remainingTriangles = triangleCount;
        while (remainingTriangles > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var batchTriangleCount = (int)Math.Min(remainingTriangles, DimensionReadBatchTriangles);
            var batchLength = batchTriangleCount * BinaryTriangleSize;
            await stream.ReadExactlyAsync(triangles.AsMemory(0, batchLength), cancellationToken);
            for (var index = 0; index < batchTriangleCount; index++)
            {
                var triangleOffset = index * BinaryTriangleSize;
                bounds.Include(ReadVector(triangles, triangleOffset + 12));
                bounds.Include(ReadVector(triangles, triangleOffset + 24));
                bounds.Include(ReadVector(triangles, triangleOffset + 36));
            }

            remainingTriangles -= (uint)batchTriangleCount;
        }

        return bounds.ToDimensions();
    }

    private static async Task<StlMeshData> ReadBinaryMeshAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var header = new byte[BinaryHeaderSize];
        await stream.ReadExactlyAsync(header, cancellationToken);
        var triangleCount = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(80, 4));
        ValidateTriangleCount(triangleCount, stream.Length);

        var vertexCount = checked((int)triangleCount * 3);
        var positions = new Vector3[vertexCount];
        var normals = new Vector3[vertexCount];
        var indices = new int[vertexCount];
        var bounds = BoundsBuilder.Empty;
        var triangle = new byte[BinaryTriangleSize];

        for (var triangleIndex = 0u; triangleIndex < triangleCount; triangleIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await stream.ReadExactlyAsync(triangle, cancellationToken);
            var normal = ReadVector(triangle, 0);
            var a = ReadVector(triangle, 12);
            var b = ReadVector(triangle, 24);
            var c = ReadVector(triangle, 36);
            if (normal.LengthSquared() < 0.000001f)
            {
                normal = SafeNormal(a, b, c);
            }

            var offset = checked((int)triangleIndex * 3);
            positions[offset] = a;
            positions[offset + 1] = b;
            positions[offset + 2] = c;
            normals[offset] = normals[offset + 1] = normals[offset + 2] = normal;
            indices[offset] = offset;
            indices[offset + 1] = offset + 1;
            indices[offset + 2] = offset + 2;
            bounds.Include(a);
            bounds.Include(b);
            bounds.Include(c);
        }

        MoveBottomToZero(positions, bounds.Minimum.Z);
        return new StlMeshData(positions, normals, indices, bounds.ToDimensions());
    }

    private static async Task<ModelDimensions> ReadAsciiDimensionsAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, true, 128 * 1024, leaveOpen: true);
        var bounds = BoundsBuilder.Empty;
        var vertexCount = 0;

        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (!TryParseVectorLine(line, "vertex", out var vertex)) continue;
            bounds.Include(vertex);
            vertexCount++;
        }

        if (vertexCount == 0 || vertexCount % 3 != 0)
        {
            throw new InvalidDataException("ASCII STL contains an invalid number of vertices.");
        }

        return bounds.ToDimensions();
    }

    private static async Task<StlMeshData> ReadAsciiMeshAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, true, 128 * 1024, leaveOpen: true);
        var positions = new List<Vector3>();
        var normals = new List<Vector3>();
        var bounds = BoundsBuilder.Empty;
        var currentNormal = Vector3.Zero;

        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (TryParseVectorLine(line, "facet normal", out var normal))
            {
                currentNormal = normal;
                continue;
            }

            if (!TryParseVectorLine(line, "vertex", out var vertex)) continue;
            positions.Add(vertex);
            normals.Add(currentNormal);
            bounds.Include(vertex);
        }

        if (positions.Count == 0 || positions.Count % 3 != 0)
        {
            throw new InvalidDataException("ASCII STL contains incomplete triangle data.");
        }

        for (var index = 0; index < positions.Count; index += 3)
        {
            if (normals[index].LengthSquared() >= 0.000001f) continue;
            var normal = SafeNormal(positions[index], positions[index + 1], positions[index + 2]);
            normals[index] = normals[index + 1] = normals[index + 2] = normal;
        }

        var positionArray = positions.ToArray();
        MoveBottomToZero(positionArray, bounds.Minimum.Z);
        return new StlMeshData(positionArray, normals.ToArray(), Enumerable.Range(0, positions.Count).ToArray(), bounds.ToDimensions());
    }

    private static Vector3 ReadVector(byte[] buffer, int offset)
    {
        var vector = new Vector3(
            BitConverter.ToSingle(buffer, offset),
            BitConverter.ToSingle(buffer, offset + 4),
            BitConverter.ToSingle(buffer, offset + 8));
        ValidateVector(vector);
        return vector;
    }

    private static bool TryParseVectorLine(string line, string prefix, out Vector3 vector)
    {
        vector = Vector3.Zero;
        var trimmed = line.Trim();
        if (!trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;
        var parts = trimmed[prefix.Length..].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3 ||
            !float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) ||
            !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y) ||
            !float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var z))
        {
            throw new InvalidDataException($"Invalid STL vector: {trimmed}");
        }

        vector = new Vector3(x, y, z);
        ValidateVector(vector);
        return true;
    }

    private static void ValidateVector(Vector3 vector)
    {
        if (!float.IsFinite(vector.X) || !float.IsFinite(vector.Y) || !float.IsFinite(vector.Z))
        {
            throw new InvalidDataException("STL contains a non-finite coordinate.");
        }
    }

    private static void ValidateTriangleCount(uint triangleCount, long streamLength)
    {
        if (triangleCount == 0 || BinaryHeaderSize + (long)triangleCount * BinaryTriangleSize > streamLength)
        {
            throw new InvalidDataException("Binary STL triangle count does not match the file length.");
        }

        if (triangleCount > int.MaxValue / 3)
        {
            throw new InvalidDataException("STL contains too many triangles to load safely.");
        }
    }

    private static Vector3 SafeNormal(Vector3 a, Vector3 b, Vector3 c)
    {
        var cross = Vector3.Cross(b - a, c - a);
        return cross.LengthSquared() < 0.000001f ? Vector3.UnitZ : Vector3.Normalize(cross);
    }

    private static void MoveBottomToZero(Vector3[] positions, float minimumZ)
    {
        if (Math.Abs(minimumZ) < 0.000001f) return;
        for (var index = 0; index < positions.Length; index++)
        {
            positions[index].Z -= minimumZ;
        }
    }

    private struct BoundsBuilder
    {
        public Vector3 Minimum;
        public Vector3 Maximum;
        private bool _hasPoint;

        public static BoundsBuilder Empty => new()
        {
            Minimum = new Vector3(float.PositiveInfinity),
            Maximum = new Vector3(float.NegativeInfinity)
        };

        public void Include(Vector3 point)
        {
            Minimum = Vector3.Min(Minimum, point);
            Maximum = Vector3.Max(Maximum, point);
            _hasPoint = true;
        }

        public ModelDimensions ToDimensions()
        {
            if (!_hasPoint) throw new InvalidDataException("STL contains no vertices.");
            var size = Maximum - Minimum;
            return new ModelDimensions(size.X, size.Y, size.Z);
        }
    }
}
