using System.Windows.Media;
using TerrainBuilder.Core.Models;

namespace TerrainBuilder.App.Services;

public interface IThumbnailService
{
    Task<ImageSource?> GetAsync(ModelLibraryItem model, CancellationToken cancellationToken = default);
}
