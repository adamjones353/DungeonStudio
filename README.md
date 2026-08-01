# DungeonStudio

DungeonStudio is a native Windows desktop application for arranging modular tabletop terrain from local STL files. Browse a terrain library, drag pieces into an interactive 3D scene, snap them to a grid, save the layout, and produce a print list without uploading anything.

> [!IMPORTANT]
> DungeonStudio is currently an early alpha. Projects should be backed up, and releases may change project or cache formats while the application is under active development.

## Features

- Recursively scans a local STL library while preserving its folder structure.
- Supports binary and ASCII STL files.
- Searches the library by filename or folder.
- Generates and caches model thumbnails locally in the background.
- Adds models by double-clicking, using the Add button, or dragging from the library into the viewport.
- Provides an interactive HelixToolkit SharpDX viewport with orbit, middle-mouse pan, zoom, a view cube, and a visible one-inch grid.
- Selects and highlights scene pieces, then supports mouse dragging, grid movement, rotation, deletion, copy, and paste-at-cursor.
- Enables or disables grid snapping with 25%, 50%, 75%, and 100% grid increments.
- Saves and loads `.terrainproject` files that reference the original STL paths.
- Saves subsequent changes directly to the loaded project file.
- Builds a print list grouped by the original STL file.
- Copies the required source STLs into a chosen print-export folder without modifying the originals.
- Reuses repeated geometry through instanced rendering.
- Generates and caches reduced-detail viewport meshes for responsive large scenes; source and exported STLs retain their full detail.
- Runs entirely on the local computer with no accounts, cloud services, uploads, telemetry, or data collection.

## Screenshot

A current screenshot or short demonstration can be added here before publishing the repository.

## Requirements

- Windows 10 or Windows 11, 64-bit
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) for building from source
- A DirectX 11-capable graphics adapter
- Visual Studio 2022 is optional; the command line is sufficient

The repository's `global.json` selects .NET SDK 8.0.418 with latest-patch roll-forward.

## Build from source

Clone the repository and open PowerShell in its root directory:

```powershell
git clone <repository-url>
cd DungeonStudio
.\Build.ps1
```

The equivalent direct commands are:

```powershell
dotnet restore .\TerrainBuilder.sln
dotnet build .\TerrainBuilder.sln --configuration Release --no-restore
dotnet test .\tests\TerrainBuilder.Tests\TerrainBuilder.Tests.csproj --configuration Release --no-build
```

## Run

```powershell
.\Run.ps1
```

Alternatively:

```powershell
dotnet run --project .\src\TerrainBuilder.App\TerrainBuilder.App.csproj
```

Choose a folder containing STL files after the application opens. The folder selection, library metadata, thumbnails, and reduced-detail viewport meshes are cached locally for later sessions.

## Publish a standalone Windows build

```powershell
dotnet publish .\src\TerrainBuilder.App\TerrainBuilder.App.csproj `
  --configuration Release `
  --runtime win-x64 `
  --self-contained true `
  --output .\artifacts\DungeonStudio-win-x64
```

The generated `artifacts` directory is intentionally excluded from source control.

## Controls

| Input | Action |
| --- | --- |
| Left-click | Select a piece |
| Left-drag selected piece | Move the piece across the placement plane |
| Right-drag | Orbit the camera |
| Middle-drag | Pan the camera |
| Mouse wheel | Zoom |
| `Delete` | Delete the selected piece |
| `Ctrl+C` / `Ctrl+V` | Copy / paste at the viewport cursor |
| `Ctrl+S` | Save the project |
| `Ctrl+O` | Open a project |
| `F` | Frame the scene |

## Project structure

- `TerrainBuilder.App` — WPF user interface, MVVM view models, HelixToolkit scene integration, dialogs, and local rendering caches.
- `TerrainBuilder.Core` — domain models, service contracts, grid snapping, and print-list logic with no WPF dependency.
- `TerrainBuilder.Infrastructure` — STL parsing, JSON library indexing, settings, project persistence, and print export.
- `TerrainBuilder.Tests` — automated tests for non-UI behavior.
- `tools/LodProbe` — a small diagnostic utility for measuring viewport mesh generation and cache loading.

## Main dependencies

- [HelixToolkit.Wpf.SharpDX](https://github.com/helix-toolkit/helix-toolkit)
- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet)
- [Microsoft.Extensions.Hosting](https://learn.microsoft.com/dotnet/core/extensions/generic-host)
- [xUnit](https://xunit.net/)

NuGet restores the exact package versions declared by the project files.

## Local data and privacy

DungeonStudio reads STL files in place and never renames, deletes, overwrites, uploads, or modifies them. Application settings and generated caches are stored beneath:

```text
%LOCALAPPDATA%\TerrainBuilder
```

Project files contain references to the original STL paths rather than embedded mesh data. Moving or deleting a referenced STL may therefore leave a missing model when a project is reopened.

## Current limitations

- Windows only.
- One library root can currently be active at a time.
- Viewport meshes are simplified for performance, so very detailed models can look noticeably rough in the scene. Source files and print exports are unaffected.
- Multi-selection, undo/redo, collision detection, measurements, layers, grouping, and combined STL/OBJ scene export are not yet implemented.
- The application and project format are still evolving.

## Contributing

Bug reports and noncommercial contributions are welcome. Please keep changes focused, preserve the MVVM/project boundaries, add tests for non-UI behavior, and verify `Build.ps1` before submitting a pull request.

Do not commit STL libraries, saved terrain projects, generated thumbnails, local SDK files, or published binaries.

## Licence

DungeonStudio is available under the [PolyForm Noncommercial License 1.0.0](LICENSE). You may use, study, modify, and redistribute the software for permitted noncommercial purposes, but commercial use and selling the software are not permitted by that licence.

Because commercial use is restricted, this is a **source-available noncommercial project**, not an OSI-approved open-source project. This distinction does not affect the goal: the program and its source remain freely available for personal, hobby, educational, charitable, and other permitted noncommercial use.

