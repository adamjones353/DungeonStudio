using CommunityToolkit.Mvvm.ComponentModel;
using TerrainBuilder.Core.Models;

namespace TerrainBuilder.App.ViewModels;

public partial class SceneViewModel
{
    public IReadOnlyList<int> GridSnapPercentOptions { get; } = [25, 50, 75, 100];

    [ObservableProperty]
    private bool isGridSnapEnabled = true;

    [ObservableProperty]
    private int gridSnapPercentage = 100;

    public double GridSnapIncrementMm => GridSizeMm * GridSnapPercentage / 100d;

    partial void OnGridSnapPercentageChanged(int value)
    {
        if (!GridSnapPercentOptions.Contains(value))
        {
            GridSnapPercentage = 100;
            return;
        }

        OnPropertyChanged(nameof(GridSnapIncrementMm));
    }

    public void MoveSelectedTo(double x, double y, double z, bool commitSceneChange = true)
    {
        if (SelectedPiece is null) return;

        var target = new TerrainVector3(x, y, Math.Max(0, z));
        if (IsGridSnapEnabled)
        {
            target = _gridSnapService.Snap(target, GridSnapIncrementMm, snapZ: false);
        }

        SelectedPiece.PositionX = target.X;
        SelectedPiece.PositionY = target.Y;
        SelectedPiece.PositionZ = target.Z;
        if (commitSceneChange) NotifySceneChanged();
    }
    public void CommitInteractiveChange() => NotifySceneChanged();
}


