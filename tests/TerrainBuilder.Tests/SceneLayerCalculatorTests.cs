using TerrainBuilder.Core.Services;

namespace TerrainBuilder.Tests;

public sealed class SceneLayerCalculatorTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(6, 0)]
    [InlineData(24.9, 0)]
    [InlineData(25, 50)]
    [InlineData(44, 50)]
    [InlineData(50, 50)]
    [InlineData(76, 100)]
    public void GetLayerElevation_GroupsNearbyPartsIntoFiftyMillimetreFloors(
        double positionZ,
        double expectedElevation)
    {
        var result = SceneLayerCalculator.GetLayerElevation(positionZ, 50);

        Assert.Equal(expectedElevation, result);
    }

    [Fact]
    public void GetLevelNumber_PreservesEmptyLevelGaps()
    {
        Assert.Equal(1, SceneLayerCalculator.GetLevelNumber(0, 50));
        Assert.Equal(2, SceneLayerCalculator.GetLevelNumber(50, 50));
        Assert.Equal(3, SceneLayerCalculator.GetLevelNumber(100, 50));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-10, 1)]
    [InlineData(5000, 1000)]
    [InlineData(double.NaN, 50)]
    public void NormalizeLayerHeight_ProtectsAgainstInvalidInput(double value, double expected)
    {
        Assert.Equal(expected, SceneLayerCalculator.NormalizeLayerHeight(value));
    }
}
