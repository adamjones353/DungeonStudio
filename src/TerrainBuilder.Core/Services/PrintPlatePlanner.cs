using TerrainBuilder.Core.Models;

namespace TerrainBuilder.Core.Services;

/// <summary>
/// Arranges print-list instances onto Creality Hi sized build plates without
/// changing or scaling the source geometry.
/// </summary>
public sealed class PrintPlatePlanner
{
    public const double BedWidthMm = 260;
    public const double BedDepthMm = 260;
    public const double BedHeightMm = 300;
    public const double EdgeMarginMm = 5;
    public const double ItemGapMm = 5;

    private const double UsableMaximumX = BedWidthMm - EdgeMarginMm;
    private const double UsableMaximumY = BedDepthMm - EdgeMarginMm;

    public PrintPlatePlan Plan(IEnumerable<PrintListItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        var work = items
            .Where(item => item.Quantity > 0)
            .SelectMany(item => Enumerable.Range(0, item.Quantity).Select(index => new WorkItem(item, index)))
            .OrderByDescending(item => Math.Max(item.Width, item.Depth))
            .ThenByDescending(item => item.Width * item.Depth)
            .ThenBy(item => item.Item.ModelName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

        var plates = new List<MutablePlate>();
        var oversize = new List<PrintPlatePlacement>();

        foreach (var item in work)
        {
            if (item.Height > BedHeightMm ||
                (item.Width > UsableWidth && item.Depth > UsableWidth) ||
                (item.Depth > UsableDepth && item.Width > UsableDepth))
            {
                var plate = new MutablePlate(plates.Count + 1);
                plates.Add(plate);
                var placement = new PrintPlatePlacement(
                    item.Item,
                    item.InstanceIndex,
                    plate.Number,
                    EdgeMarginMm,
                    EdgeMarginMm,
                    false,
                    true);
                plate.Placements.Add(placement);
                oversize.Add(placement);
                continue;
            }

            if (TryPlaceOnExistingPlate(plates, item)) continue;

            var newPlate = new MutablePlate(plates.Count + 1);
            plates.Add(newPlate);
            if (!TryPlaceOnPlate(newPlate, item))
            {
                // This can only occur for unusual dimensions that passed the
                // orientation check above. Preserve the model on its own plate
                // and flag it so the slicer/user can make the final decision.
                var placement = new PrintPlatePlacement(
                    item.Item,
                    item.InstanceIndex,
                    newPlate.Number,
                    EdgeMarginMm,
                    EdgeMarginMm,
                    false,
                    true);
                newPlate.Placements.Add(placement);
                oversize.Add(placement);
            }
        }

        return new PrintPlatePlan(
            plates.Select(plate => new PrintPlate(plate.Number, plate.Placements.ToArray())).ToArray(),
            oversize);
    }

    public static double UsableWidth => BedWidthMm - (EdgeMarginMm * 2);
    public static double UsableDepth => BedDepthMm - (EdgeMarginMm * 2);

    private static bool TryPlaceOnExistingPlate(IReadOnlyList<MutablePlate> plates, WorkItem item)
    {
        foreach (var plate in plates)
        {
            if (plate.Placements.Any(placement => placement.IsOversize)) continue;
            if (TryPlaceOnPlate(plate, item)) return true;
        }

        return false;
    }

    private static bool TryPlaceOnPlate(MutablePlate plate, WorkItem item)
    {
        foreach (var shelf in plate.Shelves)
        {
            foreach (var orientation in Orientations(item))
            {
                if (orientation.Depth > shelf.Height ||
                    shelf.CursorX + orientation.Width > UsableMaximumX + 0.0001)
                {
                    continue;
                }

                AddPlacement(plate, shelf, item, orientation);
                return true;
            }
        }

        var shelfY = plate.Shelves.Count == 0
            ? EdgeMarginMm
            : plate.Shelves[^1].Y + plate.Shelves[^1].Height + ItemGapMm;

        foreach (var orientation in Orientations(item).OrderBy(value => value.Depth))
        {
            if (EdgeMarginMm + orientation.Width > UsableMaximumX + 0.0001 ||
                shelfY + orientation.Depth > UsableMaximumY + 0.0001)
            {
                continue;
            }

            var shelf = new Shelf(shelfY, orientation.Depth, EdgeMarginMm);
            plate.Shelves.Add(shelf);
            AddPlacement(plate, shelf, item, orientation);
            return true;
        }

        return false;
    }

    private static void AddPlacement(
        MutablePlate plate,
        Shelf shelf,
        WorkItem item,
        Orientation orientation)
    {
        plate.Placements.Add(new PrintPlatePlacement(
            item.Item,
            item.InstanceIndex,
            plate.Number,
            shelf.CursorX,
            shelf.Y,
            orientation.IsRotated90Degrees,
            false));
        shelf.CursorX += orientation.Width + ItemGapMm;
    }

    private static IEnumerable<Orientation> Orientations(WorkItem item)
    {
        yield return new Orientation(item.Width, item.Depth, false);
        if (Math.Abs(item.Width - item.Depth) > 0.0001)
        {
            yield return new Orientation(item.Depth, item.Width, true);
        }
    }

    private sealed class MutablePlate(int number)
    {
        public int Number { get; } = number;
        public List<Shelf> Shelves { get; } = [];
        public List<PrintPlatePlacement> Placements { get; } = [];
    }

    private sealed class Shelf(double y, double height, double cursorX)
    {
        public double Y { get; } = y;
        public double Height { get; } = height;
        public double CursorX { get; set; } = cursorX;
    }

    private sealed record WorkItem(PrintListItem Item, int InstanceIndex)
    {
        public double Width => Math.Max(Item.Dimensions.WidthMm, 0.01);
        public double Depth => Math.Max(Item.Dimensions.DepthMm, 0.01);
        public double Height => Math.Max(Item.Dimensions.HeightMm, 0.01);
    }

    private readonly record struct Orientation(double Width, double Depth, bool IsRotated90Degrees);
}

public sealed record PrintPlatePlan(
    IReadOnlyList<PrintPlate> Plates,
    IReadOnlyList<PrintPlatePlacement> OversizePlacements);

public sealed record PrintPlate(int Number, IReadOnlyList<PrintPlatePlacement> Placements);

public sealed record PrintPlatePlacement(
    PrintListItem Item,
    int InstanceIndex,
    int PlateNumber,
    double X,
    double Y,
    bool IsRotated90Degrees,
    bool IsOversize);
