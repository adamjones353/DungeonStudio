using System.Numerics;
using System.Text;
using TerrainBuilder.Infrastructure.Stl;

namespace TerrainBuilder.Tests;

public sealed class StlParserTests
{
    [Fact]
    public async Task LoadMesh_ReadsAsciiStlAndMovesBottomToZero()
    {
        using var folder = new TemporaryFolder();
        var path = Path.Combine(folder.Path, "triangle.stl");
        await File.WriteAllTextAsync(path, """
            solid triangle
              facet normal 0 0 1
                outer loop
                  vertex 0 0 -2
                  vertex 10 0 -2
                  vertex 0 20 3
                endloop
              endfacet
            endsolid triangle
            """);

        var mesh = await new StlParser().LoadMeshAsync(path);

        Assert.Equal(3, mesh.Positions.Length);
        Assert.Equal(10, mesh.Dimensions.WidthMm, 5);
        Assert.Equal(20, mesh.Dimensions.DepthMm, 5);
        Assert.Equal(5, mesh.Dimensions.HeightMm, 5);
        Assert.Equal(0, mesh.Positions.Min(vertex => vertex.Z), 5);
    }

    [Fact]
    public async Task LoadMesh_ReadsBinaryStl()
    {
        using var folder = new TemporaryFolder();
        var path = Path.Combine(folder.Path, "binary.stl");
        await File.WriteAllBytesAsync(path, CreateBinaryTriangle(
            new Vector3(0, 0, 4),
            new Vector3(12, 0, 4),
            new Vector3(0, 8, 10)));

        var mesh = await new StlParser().LoadMeshAsync(path);

        Assert.Equal(12, mesh.Dimensions.WidthMm, 5);
        Assert.Equal(8, mesh.Dimensions.DepthMm, 5);
        Assert.Equal(6, mesh.Dimensions.HeightMm, 5);
        Assert.Equal(0, mesh.Positions.Min(vertex => vertex.Z), 5);
    }

    [Fact]
    public async Task ReadDimensions_RejectsDamagedStl()
    {
        using var folder = new TemporaryFolder();
        var path = Path.Combine(folder.Path, "damaged.stl");
        await File.WriteAllTextAsync(path, "solid damaged\nvertex 1 2 nope\nendsolid");

        await Assert.ThrowsAsync<InvalidDataException>(() => new StlParser().ReadDimensionsAsync(path));
    }

    private static byte[] CreateBinaryTriangle(Vector3 a, Vector3 b, Vector3 c)
    {
        var output = new byte[84 + 50];
        Encoding.ASCII.GetBytes("Terrain Builder test").CopyTo(output, 0);
        BitConverter.GetBytes((uint)1).CopyTo(output, 80);
        WriteVector(output, 84, Vector3.UnitZ);
        WriteVector(output, 96, a);
        WriteVector(output, 108, b);
        WriteVector(output, 120, c);
        return output;
    }

    private static void WriteVector(byte[] buffer, int offset, Vector3 vector)
    {
        BitConverter.GetBytes(vector.X).CopyTo(buffer, offset);
        BitConverter.GetBytes(vector.Y).CopyTo(buffer, offset + 4);
        BitConverter.GetBytes(vector.Z).CopyTo(buffer, offset + 8);
    }
}
