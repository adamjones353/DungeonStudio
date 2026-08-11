using TerrainBuilder.Core.Models;

namespace TerrainBuilder.Core.Services;

public static class ModelStackingService
{
    private const double GeometryEpsilon = 0.000001;
    private const double StackingEdgeMarginMm = 1;

    public static double GetPlacementElevation(
        OrientedFootprint footprint,
        IEnumerable<StackedFootprint> existingModels,
        double minimumElevation = 0)
    {
        var elevation = Math.Max(0, minimumElevation);
        foreach (var existingModel in existingModels)
        {
            if (Overlaps(footprint, existingModel.Footprint))
            {
                elevation = Math.Max(elevation, existingModel.TopElevation);
            }
        }

        return elevation;
    }

    public static bool Overlaps(OrientedFootprint first, OrientedFootprint second)
    {
        if (Math.Abs(first.Width) <= GeometryEpsilon ||
            Math.Abs(first.Depth) <= GeometryEpsilon ||
            Math.Abs(second.Width) <= GeometryEpsilon ||
            Math.Abs(second.Depth) <= GeometryEpsilon)
        {
            return false;
        }

        var firstAxes = GetAxes(first.RotationDegrees);
        var secondAxes = GetAxes(second.RotationDegrees);
        var centreDelta = new Vector2(
            second.CentreX - first.CentreX,
            second.CentreY - first.CentreY);

        return HasPositiveOverlap(firstAxes.Width, firstAxes.Depth) &&
               HasPositiveOverlap(secondAxes.Width, secondAxes.Depth);

        bool HasPositiveOverlap(Vector2 axisOne, Vector2 axisTwo) =>
            HasPositiveOverlapOnAxis(axisOne) && HasPositiveOverlapOnAxis(axisTwo);

        bool HasPositiveOverlapOnAxis(Vector2 axis)
        {
            var distance = Math.Abs(Dot(centreDelta, axis));
            var firstRadius = ProjectionRadius(first, firstAxes, axis);
            var secondRadius = ProjectionRadius(second, secondAxes, axis);
            return distance < firstRadius + secondRadius - StackingEdgeMarginMm;
        }
    }

    private static (Vector2 Width, Vector2 Depth) GetAxes(double rotationDegrees)
    {
        var radians = rotationDegrees * Math.PI / 180;
        var cosine = Math.Cos(radians);
        var sine = Math.Sin(radians);
        return (new Vector2(cosine, sine), new Vector2(-sine, cosine));
    }

    private static double ProjectionRadius(
        OrientedFootprint footprint,
        (Vector2 Width, Vector2 Depth) axes,
        Vector2 projectionAxis) =>
        Math.Abs(footprint.Width) / 2 * Math.Abs(Dot(axes.Width, projectionAxis)) +
        Math.Abs(footprint.Depth) / 2 * Math.Abs(Dot(axes.Depth, projectionAxis));

    private static double Dot(Vector2 first, Vector2 second) =>
        first.X * second.X + first.Y * second.Y;

    private readonly record struct Vector2(double X, double Y);
}
