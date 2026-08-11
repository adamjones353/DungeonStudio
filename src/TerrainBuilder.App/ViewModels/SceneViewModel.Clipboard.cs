using CommunityToolkit.Mvvm.Input;
using TerrainBuilder.Core.Models;

namespace TerrainBuilder.App.ViewModels;

public partial class SceneViewModel
{
    private ModelLibraryItem? copiedSource;
    private PlacedTerrainPiece? copiedPlacement;
    private TerrainVector3? mousePastePosition;

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void CopySelected()
    {
        if (SelectedPiece is null) return;
        copiedSource = SelectedPiece.Source;
        copiedPlacement = SelectedPiece.ToModel();
        PasteCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(HasClipboard))]
    private async Task Paste()
    {
        if (copiedSource is null || copiedPlacement is null) return;

        var offset = IsGridSnapEnabled ? GridSizeMm : GridSizeMm * 0.25;
        var targetPosition = mousePastePosition is { } mousePosition
            ? new TerrainVector3(mousePosition.X, mousePosition.Y, copiedPlacement.Position.Z)
            : new TerrainVector3(
                copiedPlacement.Position.X + offset,
                copiedPlacement.Position.Y + offset,
                copiedPlacement.Position.Z);
        var pastedPlacement = copiedPlacement with
        {
            InstanceId = Guid.NewGuid(),
            Position = targetPosition
        };

        if (await AddModelAsync(copiedSource, pastedPlacement) is not null)
        {
            copiedPlacement = pastedPlacement;
        }
    }

    public void SetMousePastePosition(double x, double y)
    {
        mousePastePosition = new TerrainVector3(x, y, 0);
    }

    private bool HasClipboard() => copiedSource is not null && copiedPlacement is not null;
}
