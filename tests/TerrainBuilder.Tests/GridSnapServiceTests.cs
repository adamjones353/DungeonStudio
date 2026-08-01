using TerrainBuilder.Core.Models;
using TerrainBuilder.Core.Services;

namespace TerrainBuilder.Tests;

public sealed class GridSnapServiceTests
{
    private readonly GridSnapService _service = new();

    [Fact]
    public void Snap_UsesGridIntersectionsAndPreservesUnsnappedHeight()
    {
        var result = _service.Snap(new TerrainVector3(38, -13, 7.5), 25.4);

        Assert.Equal(25.4, result.X, 5);
        Assert.Equal(-25.4, result.Y, 5);
        Assert.Equal(7.5, result.Z, 5);
    }

    [Theory]
    [InlineData(25, 6.35, 7, 6.35)]
    [InlineData(50, 12.7, 7, 12.7)]
    [InlineData(75, 19.05, 17, 19.05)]
    [InlineData(100, 25.4, 17, 25.4)]
    public void Snap_SupportsPercentageBasedGridIntervals(
        int percentage,
        double expectedInterval,
        double input,
        double expected)
    {
        var interval = GridSnapService.OneInchMm * percentage / 100d;
        var result = _service.Snap(new TerrainVector3(input, input, 0), interval);

        Assert.Equal(expectedInterval, interval, 5);
        Assert.Equal(expected, result.X, 5);
        Assert.Equal(expected, result.Y, 5);
    }

    [Fact]
    public void Snap_WhenVerticalSnappingEnabled_ClampsBelowGround()
    {
        var result = _service.Snap(new TerrainVector3(0, 0, -30), 25.4, snapZ: true);

        Assert.Equal(0, result.Z);
    }
}
