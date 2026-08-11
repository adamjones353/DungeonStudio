using System.Windows;
using System.Windows.Input;
using TerrainBuilder.App.ViewModels;

namespace TerrainBuilder.App;

public partial class MainWindow : Window
{
    private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext;

    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        InitializeInstancedSceneRendering();
        viewModel.Scene.ZoomExtentsRequested += (_, _) => TerrainViewport.ZoomExtents(500);
        viewModel.Scene.FocusSelectedRequested += (_, pivot) => FocusCameraOn(pivot);
    }

    private void FocusCameraOn(System.Windows.Media.Media3D.Point3D pivot)
    {
        var lookDirection = ViewModel.Scene.Camera.LookDirection;
        if (lookDirection.LengthSquared < 0.000001) return;
        ViewModel.Scene.Camera.Position = pivot - lookDirection;
        TerrainViewport.FixedRotationPoint = pivot;
        TerrainViewport.FixedRotationPointEnabled = true;
    }

    private async void LibraryItemDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ViewModel.Library.SelectedModel is not null)
        {
            await ViewModel.AddSelectedModelCommand.ExecuteAsync(ViewModel.Library.SelectedModel);
        }
    }
}
