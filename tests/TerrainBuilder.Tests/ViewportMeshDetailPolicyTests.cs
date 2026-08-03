using TerrainBuilder.Core.Services;

namespace TerrainBuilder.Tests;

public sealed class ViewportMeshDetailPolicyTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(20_000, 20_000)]
    [InlineData(87_397, 30_000)]
    [InlineData(142_962, 35_741)]
    [InlineData(306_425, 75_000)]
    [InlineData(1_000_000, 75_000)]
    public void GetTargetTriangleCount_UsesProportionalQualityWithinSafeLimits(
        int sourceTriangles,
        int expectedTarget)
    {
        Assert.Equal(expectedTarget, ViewportMeshDetailPolicy.GetTargetTriangleCount(sourceTriangles));
    }

    [Fact]
    public void GetSafetyRetryTriangleCount_DoublesDetailWithoutExceedingSource()
    {
        Assert.Equal(150_000, ViewportMeshDetailPolicy.GetSafetyRetryTriangleCount(306_425, 75_000));
        Assert.Equal(87_397, ViewportMeshDetailPolicy.GetSafetyRetryTriangleCount(87_397, 50_000));
    }
}
