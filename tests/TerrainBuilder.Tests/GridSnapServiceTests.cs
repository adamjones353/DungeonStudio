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
    [InlineData(false, 25.4, 17, 25.4)]
    [InlineData(true, 6.35, 7, 6.35)]
    public void GetSnapInterval_UsesQuarterGridOnlyForPrecisionMovement(
        bool usePrecisionSnap,
        double expectedInterval,
        double input,
        double expected)
    {
        var interval = _service.GetSnapInterval(GridSnapService.OneInchMm, usePrecisionSnap);
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

    [Fact]
    public void SnapFootprintPosition_AlignsRotatedFootprintEdgeToGrid()
    {
        var footprint = new OrientedFootprint(
            CentreX: 25.4,
            CentreY: 12.7,
            Width: 50.8,
            Depth: 25.4,
            RotationDegrees: 90);

        var result = _service.SnapFootprintPosition(
            TerrainVector3.Zero,
            footprint,
            GridSnapService.OneInchMm);
        var movedFootprint = footprint with
        {
            CentreX = footprint.CentreX + result.X,
            CentreY = footprint.CentreY + result.Y
        };

        Assert.Equal(0, movedFootprint.MinimumX % GridSnapService.OneInchMm, 5);
        Assert.Equal(0, movedFootprint.MinimumY % GridSnapService.OneInchMm, 5);
    }
}
