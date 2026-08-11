namespace TerrainBuilder.Core.Models;

public readonly record struct OrientedFootprint(
    double CentreX,
    double CentreY,
    double Width,
    double Depth,
    double RotationDegrees)
{
    public double MinimumX => CentreX - AxisAlignedHalfWidth;
    public double MinimumY => CentreY - AxisAlignedHalfDepth;

    private double AxisAlignedHalfWidth
    {
        get
        {
            var radians = RotationDegrees * Math.PI / 180;
            return Math.Abs(Width) / 2 * Math.Abs(Math.Cos(radians)) +
                   Math.Abs(Depth) / 2 * Math.Abs(Math.Sin(radians));
        }
    }

    private double AxisAlignedHalfDepth
    {
        get
        {
            var radians = RotationDegrees * Math.PI / 180;
            return Math.Abs(Width) / 2 * Math.Abs(Math.Sin(radians)) +
                   Math.Abs(Depth) / 2 * Math.Abs(Math.Cos(radians));
        }
    }
}

public readonly record struct StackedFootprint(
    OrientedFootprint Footprint,
    double BaseElevation,
    double Height)
{
    public double TopElevation => BaseElevation + Math.Abs(Height);
}
