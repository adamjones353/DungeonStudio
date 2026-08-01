using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using TerrainBuilder.App.Services;
using TerrainBuilder.Core.Models;

namespace TerrainBuilder.App.ViewModels;

public partial class LibraryTreeNodeViewModel : ObservableObject
{
    private readonly IThumbnailService? _thumbnailService;
    private bool _thumbnailAttempted;

    private LibraryTreeNodeViewModel(string name, string fullPath, bool isFolder, ModelLibraryItem? model, IThumbnailService? thumbnailService)
    {
        Name = name;
        FullPath = fullPath;
        IsFolder = isFolder;
        Model = model;
        _thumbnailService = thumbnailService;
    }

    public string Name { get; }
    public string FullPath { get; }
    public bool IsFolder { get; }
    public bool IsModel => !IsFolder;
    public ModelLibraryItem? Model { get; }
    public string DimensionsDisplay => Model?.DimensionsDisplay ?? string.Empty;
    public ObservableCollection<LibraryTreeNodeViewModel> Children { get; } = [];

    [ObservableProperty]
    private ImageSource? thumbnail;

    [ObservableProperty]
    private bool isExpanded;

    [ObservableProperty]
    private bool isLoadingThumbnail;

    public static LibraryTreeNodeViewModel Folder(string name, string fullPath) =>
        new(name, fullPath, true, null, null);

    public static LibraryTreeNodeViewModel ModelNode(ModelLibraryItem model, IThumbnailService thumbnailService) =>
        new(model.FileName, model.FullPath, false, model, thumbnailService);

    public async Task EnsureThumbnailAsync()
    {
        if (IsFolder || _thumbnailAttempted || _thumbnailService is null || Model is null) return;
        _thumbnailAttempted = true;
        IsLoadingThumbnail = true;
        try
        {
            Thumbnail = await _thumbnailService.GetAsync(Model);
        }
        finally
        {
            IsLoadingThumbnail = false;
        }
    }
}
