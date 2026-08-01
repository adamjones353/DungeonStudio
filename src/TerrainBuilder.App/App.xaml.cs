using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TerrainBuilder.App.Services;
using TerrainBuilder.App.ViewModels;
using TerrainBuilder.Core.Services;
using TerrainBuilder.Infrastructure.Export;
using TerrainBuilder.Infrastructure.Library;
using TerrainBuilder.Infrastructure.Projects;
using TerrainBuilder.Infrastructure.Settings;
using TerrainBuilder.Infrastructure.Stl;

namespace TerrainBuilder.App;

public partial class App : Application
{
    private readonly IHost _host;

    public App()
    {
        _host = new HostBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<IStlParser, StlParser>();
                services.AddSingleton<ILibraryIndexService, JsonLibraryIndexService>();
                services.AddSingleton<IProjectStore, JsonProjectStore>();
                services.AddSingleton<IPrintExportService, PrintExportService>();
                services.AddSingleton<IAppSettingsStore, JsonAppSettingsStore>();
                services.AddSingleton<GridSnapService>();
                services.AddSingleton<PrintListService>();
                services.AddSingleton<IFileDialogService, WindowsFileDialogService>();
                services.AddSingleton<IThumbnailService, StlThumbnailService>();
                services.AddSingleton<IHelixMeshCache, HelixMeshCache>();
                services.AddSingleton<ModelLibraryViewModel>();
                services.AddSingleton<SceneViewModel>();
                services.AddSingleton<PrintListViewModel>();
                services.AddSingleton<MainWindowViewModel>();
                services.AddSingleton<MainWindow>();
            })
            .Build();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        await _host.StartAsync();
        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
        await _host.Services.GetRequiredService<MainWindowViewModel>().InitializeAsync();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        await _host.StopAsync();
        _host.Dispose();
        base.OnExit(e);
    }
}


