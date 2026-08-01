using TerrainBuilder.Core.Models;

namespace TerrainBuilder.Core.Services;

public sealed class PrintListService
{
    public IReadOnlyList<PrintListItem> Generate(
        IEnumerable<PlacedTerrainPiece> pieces,
        IEnumerable<ModelLibraryItem> library)
    {
        var metadata = library.ToDictionary(item => item.FullPath, StringComparer.OrdinalIgnoreCase);

        return pieces
            .GroupBy(piece => piece.SourceStlPath, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                metadata.TryGetValue(group.Key, out var model);
                return new PrintListItem
                {
                    ModelName = model?.DisplayName ?? group.First().DisplayName,
                    FullPath = group.Key,
                    SourceFolder = model?.FolderPath ?? Path.GetDirectoryName(group.Key) ?? string.Empty,
                    Dimensions = model?.Dimensions ?? ModelDimensions.Empty,
                    Quantity = group.Count()
                };
            })
            .OrderBy(item => item.ModelName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.FullPath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
