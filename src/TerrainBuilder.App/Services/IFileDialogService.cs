namespace TerrainBuilder.App.Services;

public interface IFileDialogService
{
    string? ChooseLibraryFolder();
    string? ChooseProjectToOpen();
    string? ChooseProjectToSave(string suggestedName);
    string? ChoosePrintExportParentFolder(string projectName);
}

