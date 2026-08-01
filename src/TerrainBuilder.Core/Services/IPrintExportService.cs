using TerrainBuilder.Core.Models;

namespace TerrainBuilder.Core.Services;

public interface IPrintExportService
{
    Task<PrintExportResult> ExportAsync(
        IEnumerable<PrintListItem> printItems,
        string destinationParentFolder,
        string projectName,
        CancellationToken cancellationToken = default);
}
