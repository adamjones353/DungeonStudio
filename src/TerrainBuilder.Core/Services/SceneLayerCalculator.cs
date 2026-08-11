namespace TerrainBuilder.Core.Services;

public static class SceneLayerCalculator
{
    public const double DefaultLayerHeightMm = 50;
    public const double MinimumLayerHeightMm = 1;
    public const double MaximumLayerHeightMm = 1000;

    public static double GetLayerElevation(double positionZ, double layerHeightMm)
    {
        var height = NormalizeLayerHeight(layerHeightMm);
        var elevation = Math.Max(0, positionZ);
        var levelIndex = Math.Round(elevation / height, MidpointRounding.AwayFromZero);
        return levelIndex * height;
    }

    public static int GetLevelNumber(double layerElevationMm, double layerHeightMm)
    {
        var height = NormalizeLayerHeight(layerHeightMm);
        return Math.Max(1, (int)Math.Round(
            Math.Max(0, layerElevationMm) / height,
            MidpointRounding.AwayFromZero) + 1);
    }

    public static double GetContainingLevelElevation(double positionZ, double layerHeightMm)
    {
        var height = NormalizeLayerHeight(layerHeightMm);
        return Math.Floor(Math.Max(0, positionZ) / height) * height;
    }

    public static bool IsOnSameLevel(double firstElevation, double secondElevation, double layerHeightMm) =>
        GetLayerElevation(firstElevation, layerHeightMm) ==
        GetLayerElevation(secondElevation, layerHeightMm);

    public static double NormalizeLayerHeight(double layerHeightMm) =>
        double.IsFinite(layerHeightMm)
            ? Math.Clamp(layerHeightMm, MinimumLayerHeightMm, MaximumLayerHeightMm)
            : DefaultLayerHeightMm;
}
