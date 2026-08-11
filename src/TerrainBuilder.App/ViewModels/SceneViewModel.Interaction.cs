using CommunityToolkit.Mvvm.ComponentModel;
using TerrainBuilder.Core.Models;

namespace TerrainBuilder.App.ViewModels;

public partial class SceneViewModel
{
    [ObservableProperty]
    private bool isGridSnapEnabled = true;

    public void MoveSelectedTo(
        double x,
        double y,
        double z,
        bool usePrecisionSnap = false,
        bool commitSceneChange = true)
    {
        if (SelectedPiece is null) return;

        var target = new TerrainVector3(x, y, Math.Max(0, z));
        if (IsGridSnapEnabled)
        {
            var interval = _gridSnapService.GetSnapInterval(GridSizeMm, usePrecisionSnap);
            target = _gridSnapService.SnapFootprintPosition(
                target,
                SelectedPiece.GetFootprintAt(target.X, target.Y),
                interval);
        }

        SelectedPiece.PositionX = target.X;
        SelectedPiece.PositionY = target.Y;
        SelectedPiece.PlacementBaseElevation = target.Z;
        SelectedPiece.PositionZ = GetStackedElevation(SelectedPiece, target.Z);
        if (commitSceneChange) NotifySceneChanged();
    }
    public void CommitInteractiveChange() => NotifySceneChanged();
}
