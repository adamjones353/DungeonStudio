using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TerrainBuilder.App.Services;
using TerrainBuilder.Core.Models;
using TerrainBuilder.Core.Services;

namespace TerrainBuilder.App.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly IFileDialogService _dialogs;
    private readonly IProjectStore _projectStore;
    private readonly IPrintExportService _printExportService;
    private readonly IAppSettingsStore _settingsStore;
    private readonly PrintListService _printListService;
    private string? currentProjectPath;

    public MainWindowViewModel(
        ModelLibraryViewModel library,
        SceneViewModel scene,
        PrintListViewModel printList,
        IFileDialogService dialogs,
        IProjectStore projectStore,
        IPrintExportService printExportService,
        IAppSettingsStore settingsStore,
        PrintListService printListService)
    {
        Library = library;
        Scene = scene;
        PrintList = printList;
        _dialogs = dialogs;
        _projectStore = projectStore;
        _printExportService = printExportService;
        _settingsStore = settingsStore;
        _printListService = printListService;
        Scene.SceneChanged += (_, _) =>
        {
            RefreshPrintList();
            IsDirty = true;
        };
    }

    public ModelLibraryViewModel Library { get; }
    public SceneViewModel Scene { get; }
    public PrintListViewModel PrintList { get; }
    public string UnitDisplay => "Millimetres (mm)";
    public string GridDisplay => $"{Scene.GridSizeMm:0.0} mm - 1 inch";

    [ObservableProperty]
    private string projectName = "Untitled Terrain";

    [ObservableProperty]
    private string statusMessage = "Ready - choose an STL library folder to begin.";

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private bool isDirty;

    public async Task InitializeAsync()
    {
        var settings = await _settingsStore.LoadAsync();
        if (string.IsNullOrWhiteSpace(settings.LastLibraryFolder)) return;
        if (!Directory.Exists(settings.LastLibraryFolder))
        {
            StatusMessage = "The saved STL library folder is no longer available. Choose a new folder to update it.";
            return;
        }

        StatusMessage = "Reopening the saved STL library...";
        await LoadLibraryAsync(settings.LastLibraryFolder);
    }

    [RelayCommand]
    private async Task BrowseLibraryAsync()
    {
        var folder = _dialogs.ChooseLibraryFolder();
        if (folder is null) return;
        await LoadLibraryAsync(folder);
    }

    [RelayCommand]
    private async Task RefreshLibraryAsync()
    {
        if (Library.RootFolder is null) return;
        await LoadLibraryAsync(Library.RootFolder);
    }

    [RelayCommand]
    private async Task AddSelectedModelAsync(ModelLibraryItem? requestedModel = null)
    {
        var model = requestedModel ?? Library.SelectedModel;
        if (model is null || !model.IsValid)
        {
            StatusMessage = "Select a valid STL model first.";
            return;
        }

        await RunBusyAsync(
            () => Scene.AddModelAsync(model),
            $"Added {model.DisplayName}.",
            "Could not add the selected model");
    }

    [RelayCommand]
    private async Task SaveProjectAsync()
    {
        var filePath = currentProjectPath ?? _dialogs.ChooseProjectToSave(ProjectName);
        if (filePath is null) return;
        var project = new TerrainProject
        {
            Name = ProjectName,
            LibraryFolder = Library.RootFolder,
            GridSizeMm = Scene.GridSizeMm,
            IsGridSnapEnabled = Scene.IsGridSnapEnabled,
            GridSnapPercentage = Scene.GridSnapPercentage,
            Pieces = Scene.Pieces.Select(piece => piece.ToModel()).ToArray()
        };

        await RunBusyAsync(
            async () =>
            {
                await _projectStore.SaveAsync(filePath, project);
                currentProjectPath = filePath;
                IsDirty = false;
            },
            $"Saved {Path.GetFileName(filePath)}.",
            "Could not save the project");
    }

    [RelayCommand]
    private async Task LoadProjectAsync()
    {
        var filePath = _dialogs.ChooseProjectToOpen();
        if (filePath is null) return;
        await RunBusyAsync(
            async () =>
            {
                var project = await _projectStore.LoadAsync(filePath);
                if (project.LibraryFolder is not null && Directory.Exists(project.LibraryFolder))
                {
                    await Library.LoadFolderAsync(project.LibraryFolder);
                }

                Scene.Clear();
                Scene.GridSizeMm = project.GridSizeMm;
                Scene.IsGridSnapEnabled = project.IsGridSnapEnabled;
                Scene.GridSnapPercentage = project.GridSnapPercentage is 25 or 50 or 75 or 100
                    ? project.GridSnapPercentage
                    : 100;
                var models = Library.Models.ToDictionary(item => item.FullPath, StringComparer.OrdinalIgnoreCase);
                var missing = 0;
                foreach (var placement in project.Pieces)
                {
                    models.TryGetValue(placement.SourceStlPath, out var model);
                    if (!await Scene.RestorePieceAsync(placement, model)) missing++;
                }

                ProjectName = project.Name;
                currentProjectPath = filePath;
                IsDirty = false;
                StatusMessage = missing == 0
                    ? $"Opened {Path.GetFileName(filePath)}."
                    : $"Opened project with {missing} missing STL reference(s).";
                Scene.FrameSceneCommand.Execute(null);
            },
            null,
            "Could not open the project");
    }

    [RelayCommand]
    private async Task ExportPrintStlsAsync()
    {
        if (PrintList.Items.Count == 0)
        {
            StatusMessage = "Add at least one model to the scene before exporting STL files.";
            return;
        }

        var parentFolder = _dialogs.ChoosePrintExportParentFolder(ProjectName);
        if (parentFolder is null) return;

        IsBusy = true;
        try
        {
            var result = await _printExportService.ExportAsync(
                PrintList.Items.ToArray(),
                parentFolder,
                ProjectName);
            StatusMessage = result.MissingFiles.Count == 0
                ? $"Exported {result.FilesCopied:N0} STL file(s) to {result.ExportFolder}."
                : $"Exported {result.FilesCopied:N0} STL file(s) to {result.ExportFolder}; {result.MissingFiles.Count:N0} source file(s) were missing.";
        }
        catch (Exception exception)
        {
            StatusMessage = $"Could not export STL files: {exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadLibraryAsync(string folder)
    {
        IsBusy = true;
        try
        {
            await Library.LoadFolderAsync(folder);
            await _settingsStore.SaveAsync(new TerrainBuilderSettings
            {
                LastLibraryFolder = Library.RootFolder
            });
            StatusMessage = $"Indexed {Library.Models.Count:N0} STL files. This folder will reopen next time.";
        }
        catch (Exception exception)
        {
            StatusMessage = $"Could not scan the library: {exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RefreshPrintList()
    {
        var items = _printListService.Generate(
            Scene.Pieces.Select(piece => piece.ToModel()),
            Library.Models);
        PrintList.ReplaceWith(items);
    }

    private async Task RunBusyAsync(Func<Task> operation, string? successMessage, string errorPrefix)
    {
        IsBusy = true;
        try
        {
            await operation();
            if (successMessage is not null) StatusMessage = successMessage;
        }
        catch (Exception exception)
        {
            StatusMessage = $"{errorPrefix}: {exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RunBusyAsync<T>(Func<Task<T>> operation, string successMessage, string errorPrefix)
    {
        await RunBusyAsync(async () => { _ = await operation(); }, successMessage, errorPrefix);
    }
}




