using TerrainBuilder.Core.Models;

namespace TerrainBuilder.Core.Services;

public sealed class GridSnapService
{
    public const double OneInchMm = 25.4;

    public TerrainVector3 Snap(TerrainVector3 position, double gridSizeMm, bool snapZ = false)
    {
        if (gridSizeMm <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(gridSizeMm), "Grid size must be greater than zero.");
        }

        return new TerrainVector3(
            SnapToGridLine(position.X, gridSizeMm),
            SnapToGridLine(position.Y, gridSizeMm),
            snapZ ? Math.Max(0, SnapToGridLine(position.Z, gridSizeMm)) : Math.Max(0, position.Z));
    }

    private static double SnapToGridLine(double value, double interval) =>
        Math.Round(value / interval, MidpointRounding.AwayFromZero) * interval;
}


