using System.Windows;
using HelixToolkit.SharpDX;
using HelixToolkit.Wpf.SharpDX;
using TerrainBuilder.App.ViewModels;

namespace TerrainBuilder.App;

public partial class MainWindow
{
    private void ViewportMouseDown3D(object sender, RoutedEventArgs e)
    {
        if (e is MouseDown3DEventArgs mouseEvent &&
            mouseEvent.HitTestResult?.ModelHit is Element3D element &&
            element.DataContext is ScenePieceViewModel piece)
        {
            ViewModel.Scene.Select(piece);
        }
    }
}
