using TerrainBuilder.Core.Models;
using TerrainBuilder.Core.Services;

namespace TerrainBuilder.Tests;

public sealed class ModelStackingServiceTests
{
    [Fact]
    public void GetPlacementElevation_UsesTopOfHighestOverlappingModel()
    {
        var moving = Footprint(0, 0, 10, 10);
        var models = new[]
        {
            new StackedFootprint(Footprint(2, 0, 10, 10), 0, 12),
            new StackedFootprint(Footprint(-2, 0, 10, 10), 12, 8),
            new StackedFootprint(Footprint(100, 100, 10, 10), 50, 25)
        };

        var elevation = ModelStackingService.GetPlacementElevation(moving, models);

        Assert.Equal(20, elevation);
    }

    [Fact]
    public void GetPlacementElevation_PreservesMinimumElevationWhenItIsHigher()
    {
        var moving = Footprint(0, 0, 10, 10);
        var models = new[]
        {
            new StackedFootprint(Footprint(0, 0, 10, 10), 0, 12)
        };

        var elevation = ModelStackingService.GetPlacementElevation(moving, models, minimumElevation: 30);

        Assert.Equal(30, elevation);
    }

    [Fact]
    public void Overlaps_AccountsForRotationInsteadOfUsingAxisAlignedBounds()
    {
        var first = Footprint(0, 0, 20, 2, rotation: 45);
        var second = Footprint(0, 8, 20, 2, rotation: 45);

        Assert.False(ModelStackingService.Overlaps(first, second));

        second = second with { CentreY = 1 };
        Assert.True(ModelStackingService.Overlaps(first, second));
    }

    [Fact]
    public void Overlaps_AccountsForScaledFootprintDimensions()
    {
        var existing = Footprint(0, 0, 10, 10);
        var unscaled = Footprint(11, 0, 10, 10);
        var scaled = unscaled with { Width = unscaled.Width * 2 };

        Assert.False(ModelStackingService.Overlaps(unscaled, existing));
        Assert.True(ModelStackingService.Overlaps(scaled, existing));
    }

    [Fact]
    public void Overlaps_TouchingEdgesDoNotCountAsOverlap()
    {
        var first = Footprint(0, 0, 10, 10);
        var second = Footprint(10, 0, 10, 10);

        Assert.False(ModelStackingService.Overlaps(first, second));
    }

    [Fact]
    public void GetPlacementElevation_IgnoresShallowEdgeOverlapWithinMargin()
    {
        var moving = Footprint(9.25, 0, 10, 10);
        var existing = new StackedFootprint(Footprint(0, 0, 10, 10), 0, 12);

        var elevation = ModelStackingService.GetPlacementElevation(moving, [existing]);

        Assert.Equal(0, elevation);
    }

    [Fact]
    public void Overlaps_CountsIntersectionBeyondEdgeMargin()
    {
        var first = Footprint(0, 0, 10, 10);
        var second = Footprint(8.75, 0, 10, 10);

        Assert.True(ModelStackingService.Overlaps(first, second));
    }

    [Fact]
    public void TopElevation_AccountsForScaleRepresentedInHeightAndNegativeScale()
    {
        var model = new StackedFootprint(Footprint(0, 0, 10, 10), 5, -24);

        Assert.Equal(29, model.TopElevation);
    }

    private static OrientedFootprint Footprint(
        double centreX,
        double centreY,
        double width,
        double depth,
        double rotation = 0) =>
        new(centreX, centreY, width, depth, rotation);
}
