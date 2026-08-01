namespace TerrainBuilder.Core.Models;

public readonly record struct ModelDimensions(double WidthMm, double DepthMm, double HeightMm)
{
    public static ModelDimensions Empty => new(0, 0, 0);

    public string ToDisplayString() => $"{WidthMm:0.#} × {DepthMm:0.#} × {HeightMm:0.#} mm";

    public override string ToString() => ToDisplayString();
}
