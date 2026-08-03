using TerrainBuilder.Core.Models;
using TerrainBuilder.Core.Services;

namespace TerrainBuilder.Tests;

public sealed class PrintPlatePlannerTests
{
    [Fact]
    public void Plan_FitsFour120MillimetreTilesOnOneCrealityHiPlate()
    {
        var plan = new PrintPlatePlanner().Plan([Item("floor.stl", 120, 120, 5, 4)]);

        var plate = Assert.Single(plan.Plates);
        Assert.Equal(4, plate.Placements.Count);
        Assert.Empty(plan.OversizePlacements);
        Assert.All(plate.Placements, placement =>
        {
            Assert.InRange(placement.X, 5, 135);
            Assert.InRange(placement.Y, 5, 135);
        });
    }

    [Fact]
    public void Plan_SplitsCopiesAcrossAsManyPlatesAsNeeded()
    {
        var plan = new PrintPlatePlanner().Plan([Item("wall.stl", 130, 130, 20, 3)]);

        Assert.Equal(3, plan.Plates.Count);
        Assert.All(plan.Plates, plate => Assert.Single(plate.Placements));
        Assert.Equal([1, 2, 3], plan.Plates.Select(plate => plate.Number));
    }

    [Fact]
    public void Plan_RotatesRectangularPieceWhenThatFitsAnExistingShelf()
    {
        var items = new[]
        {
            Item("wide.stl", 180, 100, 20, 1),
            Item("turn-me.stl", 100, 60, 20, 1)
        };

        var plan = new PrintPlatePlanner().Plan(items);

        var plate = Assert.Single(plan.Plates);
        Assert.Equal(2, plate.Placements.Count);
        Assert.Contains(plate.Placements, placement =>
            placement.Item.FullPath == "turn-me.stl" && placement.IsRotated90Degrees);
    }

    [Fact]
    public void Plan_FlagsPiecesLargerThanSafeAreaOrHeight()
    {
        var plan = new PrintPlatePlanner().Plan(
        [
            Item("too-wide.stl", 260, 260, 10, 1),
            Item("too-tall.stl", 20, 20, 301, 1)
        ]);

        Assert.Equal(2, plan.OversizePlacements.Count);
        Assert.All(plan.OversizePlacements, placement => Assert.True(placement.IsOversize));
    }

    private static PrintListItem Item(string path, double width, double depth, double height, int quantity) => new()
    {
        ModelName = Path.GetFileNameWithoutExtension(path),
        FullPath = path,
        SourceFolder = ".",
        Dimensions = new ModelDimensions(width, depth, height),
        Quantity = quantity
    };
}
