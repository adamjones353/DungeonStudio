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
    private readonly HashSet<string> _indexedPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, LibraryTreeNodeViewModel> _foldersByPath = new(StringComparer.OrdinalIgnoreCase);
    private LibraryTreeNodeViewModel? _treeRoot;

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

    [ObservableProperty]
    private string? currentIndexedFile;

    public int VisibleModelCount => FilteredModels().Count();

    partial void OnSearchTextChanged(string value) => RebuildTree();

    partial void OnSelectedNodeChanged(LibraryTreeNodeViewModel? value)
    {
        SelectedModel = value?.Model;
    }

    public async Task LoadFolderAsync(string folderPath, CancellationToken cancellationToken = default)
    {
        var fullPath = Path.GetFullPath(folderPath);
        RootFolder = fullPath;
        Models.Clear();
        _indexedPaths.Clear();
        RebuildTree();
        IsScanning = true;
        ScanProgress = 0;
        CurrentIndexedFile = null;
        try
        {
            var progress = new Progress<LibraryScanProgress>(value =>
            {
                ScanProgress = value.Percentage;
                CurrentIndexedFile = value.CurrentFilePath is null
                    ? null
                    : Path.GetRelativePath(fullPath, value.CurrentFilePath);
                if (value.CompletedItems is { Count: > 0 })
                {
                    AddIndexedModels(value.CompletedItems);
                }
            });
            var models = await _indexService.ScanAsync(fullPath, progress, cancellationToken);
            AddIndexedModels(models);
            RebuildTree();
        }
        finally
        {
            IsScanning = false;
            CurrentIndexedFile = null;
        }
    }

    private IEnumerable<ModelLibraryItem> FilteredModels() =>
        string.IsNullOrWhiteSpace(SearchText)
            ? Models
            : Models.Where(MatchesSearch);

    private bool MatchesSearch(ModelLibraryItem model) =>
        string.IsNullOrWhiteSpace(SearchText) ||
        model.FileName.Contains(SearchText, StringComparison.CurrentCultureIgnoreCase) ||
        model.FolderPath.Contains(SearchText, StringComparison.CurrentCultureIgnoreCase);

    private void RebuildTree()
    {
        RootNodes.Clear();
        _foldersByPath.Clear();
        _treeRoot = null;
        if (RootFolder is null)
        {
            OnPropertyChanged(nameof(VisibleModelCount));
            return;
        }

        var rootName = new DirectoryInfo(RootFolder).Name;
        _treeRoot = LibraryTreeNodeViewModel.Folder(
            string.IsNullOrWhiteSpace(SearchText) ? rootName : $"Search results for \"{SearchText}\"",
            RootFolder);
        _treeRoot.IsExpanded = true;
        _foldersByPath[RootFolder] = _treeRoot;

        foreach (var model in FilteredModels().OrderBy(item => item.FullPath, StringComparer.CurrentCultureIgnoreCase))
        {
            AddModelToTree(model);
        }

        SortChildren(_treeRoot);
        RootNodes.Add(_treeRoot);
        OnPropertyChanged(nameof(VisibleModelCount));
    }

    private void AddIndexedModels(IEnumerable<ModelLibraryItem> models)
    {
        var added = false;
        foreach (var model in models)
        {
            if (!_indexedPaths.Add(model.FullPath)) continue;
            Models.Add(model);
            if (MatchesSearch(model)) AddModelToTree(model);
            added = true;
        }

        if (added) OnPropertyChanged(nameof(VisibleModelCount));
    }

    private void AddModelToTree(ModelLibraryItem model)
    {
        if (_treeRoot is null || RootFolder is null) return;
        var parent = _treeRoot;
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            var relativeFolder = Path.GetRelativePath(RootFolder, model.FolderPath);
            if (relativeFolder != ".")
            {
                var currentPath = RootFolder;
                foreach (var segment in relativeFolder.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
                {
                    currentPath = Path.Combine(currentPath, segment);
                    if (!_foldersByPath.TryGetValue(currentPath, out var child))
                    {
                        child = LibraryTreeNodeViewModel.Folder(segment, currentPath);
                        parent.Children.Add(child);
                        _foldersByPath.Add(currentPath, child);
                    }
                    parent = child;
                }
            }
        }

        parent.Children.Add(LibraryTreeNodeViewModel.ModelNode(model, _thumbnailService));
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
