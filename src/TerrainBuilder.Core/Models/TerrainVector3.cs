namespace TerrainBuilder.Core.Models;

public readonly record struct TerrainVector3(double X, double Y, double Z)
{
    public static TerrainVector3 Zero => new(0, 0, 0);
}
