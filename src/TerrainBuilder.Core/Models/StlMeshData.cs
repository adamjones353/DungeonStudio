using System.Numerics;

namespace TerrainBuilder.Core.Models;

public sealed record StlMeshData(
    Vector3[] Positions,
    Vector3[] Normals,
    int[] Indices,
    ModelDimensions Dimensions);
