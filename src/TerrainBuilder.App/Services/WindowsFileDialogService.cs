using Microsoft.Win32;

namespace TerrainBuilder.App.Services;

public sealed class WindowsFileDialogService : IFileDialogService
{
    public string? ChooseLibraryFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Choose an STL library folder",
            Multiselect = false
        };
        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }

    public string? ChoosePrintExportParentFolder(string projectName)
    {
        var dialog = new OpenFolderDialog
        {
            Title = $"Choose where to create the {projectName} print-export folder",
            Multiselect = false
        };
        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }

    public string? ChooseProjectToOpen()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Open DungeonStudio project",
            Filter = "DungeonStudio project (*.terrainproject)|*.terrainproject",
            CheckFileExists = true
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? ChooseProjectToSave(string suggestedName)
    {
        var safeName = string.Concat(suggestedName.Select(character =>
            Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
        var dialog = new SaveFileDialog
        {
            Title = "Save DungeonStudio project",
            Filter = "DungeonStudio project (*.terrainproject)|*.terrainproject",
            DefaultExt = ".terrainproject",
            AddExtension = true,
            OverwritePrompt = true,
            FileName = string.IsNullOrWhiteSpace(safeName) ? "Untitled Terrain" : safeName
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}

