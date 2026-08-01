using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using TerrainBuilder.App.Services;
using TerrainBuilder.Core.Models;
using TerrainBuilder.Core.Services;

namespace TerrainBuilder.App.ViewModels;

public partial class ModelLibraryViewModel : ObservableObject
{
    private readonly ILibraryIndexService _indexService;
    private readonly IThumbnailService _thumbnailService;

    public ModelLibraryViewModel(ILibraryIndexService indexService, IThumbnailService thumbnailService)
    {
        _indexService = indexService;
        _thumbnailService = thumbnailService;
    }

    public ObservableCollection<ModelLibraryItem> Models { get; } = [];
    public ObservableCollection<LibraryTreeNodeViewModel> RootNodes { get; } = [];

    [ObservableProperty]
    private ModelLibraryItem? selectedModel;

    [ObservableProperty]
    private LibraryTreeNodeViewModel? selectedNode;

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private string? rootFolder;

    [ObservableProperty]
    private bool isScanning;

    [ObservableProperty]
    private int scanProgress;

    public int VisibleModelCount => FilteredModels().Count();

    partial void OnSearchTextChanged(string value) => RebuildTree();

    partial void OnSelectedNodeChanged(LibraryTreeNodeViewModel? value)
    {
        SelectedModel = value?.Model;
    }

    public async Task LoadFolderAsync(string folderPath, CancellationToken cancellationToken = default)
    {
        IsScanning = true;
        ScanProgress = 0;
        try
        {
            var progress = new Progress<int>(value => ScanProgress = value);
            var models = await _indexService.ScanAsync(folderPath, progress, cancellationToken);
            Models.Clear();
            foreach (var model in models) Models.Add(model);
            RootFolder = Path.GetFullPath(folderPath);
            RebuildTree();
        }
        finally
        {
            IsScanning = false;
        }
    }

    private IEnumerable<ModelLibraryItem> FilteredModels()
    {
        if (string.IsNullOrWhiteSpace(SearchText)) return Models;
        return Models.Where(model =>
            model.FileName.Contains(SearchText, StringComparison.CurrentCultureIgnoreCase) ||
            model.FolderPath.Contains(SearchText, StringComparison.CurrentCultureIgnoreCase));
    }

    private void RebuildTree()
    {
        RootNodes.Clear();
        if (RootFolder is null)
        {
            OnPropertyChanged(nameof(VisibleModelCount));
            return;
        }

        var rootName = new DirectoryInfo(RootFolder).Name;
        var root = LibraryTreeNodeViewModel.Folder(
            string.IsNullOrWhiteSpace(SearchText) ? rootName : $"Search results for “{SearchText}”",
            RootFolder);
        root.IsExpanded = true;

        foreach (var model in FilteredModels().OrderBy(item => item.FullPath, StringComparer.CurrentCultureIgnoreCase))
        {
            var parent = root;
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                var relativeFolder = Path.GetRelativePath(RootFolder, model.FolderPath);
                if (relativeFolder != ".")
                {
                    var currentPath = RootFolder;
                    foreach (var segment in relativeFolder.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
                    {
                        currentPath = Path.Combine(currentPath, segment);
                        var child = parent.Children.FirstOrDefault(node =>
                            node.IsFolder && string.Equals(node.Name, segment, StringComparison.CurrentCultureIgnoreCase));
                        if (child is null)
                        {
                            child = LibraryTreeNodeViewModel.Folder(segment, currentPath);
                            parent.Children.Add(child);
                        }
                        parent = child;
                    }
                }
            }
            parent.Children.Add(LibraryTreeNodeViewModel.ModelNode(model, _thumbnailService));
        }

        SortChildren(root);
        RootNodes.Add(root);
        OnPropertyChanged(nameof(VisibleModelCount));
    }

    private static void SortChildren(LibraryTreeNodeViewModel node)
    {
        var sorted = node.Children
            .OrderByDescending(child => child.IsFolder)
            .ThenBy(child => child.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        node.Children.Clear();
        foreach (var child in sorted)
        {
            SortChildren(child);
            node.Children.Add(child);
        }
    }
}
