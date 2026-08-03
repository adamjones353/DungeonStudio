namespace TerrainBuilder.Core.Services;

public static class ViewportMeshDetailPolicy
{
    public const int MinimumTriangleBudget = 30_000;
    public const int MaximumTriangleBudget = 75_000;
    public const double RetainedTriangleRatio = 0.25;

    public static int GetTargetTriangleCount(int sourceTriangleCount)
    {
        if (sourceTriangleCount <= 0) return 0;
        if (sourceTriangleCount <= MinimumTriangleBudget) return sourceTriangleCount;

        var proportionalTarget = (int)Math.Ceiling(sourceTriangleCount * RetainedTriangleRatio);
        return Math.Min(
            sourceTriangleCount,
            Math.Clamp(proportionalTarget, MinimumTriangleBudget, MaximumTriangleBudget));
    }

    public static int GetSafetyRetryTriangleCount(int sourceTriangleCount, int firstTarget) =>
        Math.Min(sourceTriangleCount, Math.Max(firstTarget, firstTarget * 2));
}
