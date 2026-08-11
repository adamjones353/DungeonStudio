# DungeonStudio implementation plan

## Phase 1 — native foundation (complete)

- Four-project .NET 8 solution and WPF MVVM shell.
- HelixToolkit SharpDX viewport, perspective camera, lighting, and one-inch grid.
- Recursive local STL library scanning, indexing, caching, listing, and search.
- Binary and ASCII STL support with invalid-file isolation.
- Basic placement, selection, movement, rotation, deletion, project persistence, and print list.
- Automated tests for non-UI behavior.

## Current alpha improvements

- Folder-tree library browser with locally cached thumbnails.
- Drag-and-drop placement from the library into the scene.
- Resizable library and inspector panels.
- Mouse dragging for placed pieces and middle-mouse camera panning.
- Full-grid snapping with temporary 25% precision snapping while Ctrl is held during dragging.
- Selection highlighting, copy/paste at the viewport cursor, and camera focus controls.
- Direct saving back to an opened project file.
- Print-export folder containing copies of required source STL files.
- Instanced rendering and cached reduced-detail viewport meshes for larger scenes.

## Planned scene workflow

- Transparent mouse-follow placement previews and repeated placement counts.
- Multi-selection, duplicate, undo, and redo.
- Configurable grid sizes and rotation snapping presets.
- Project autosave, recovery files, recent projects, and missing-file relinking.
- Print-list sorting, filtering, and CSV/JSON/text export.

## Planned advanced tools

- Perspective, top, front, side, and isometric camera presets.
- Grid, edge, shadow, and material toggles with saved camera state.
- Vertical surface snapping, stacking, optional collision detection, and overlap highlighting.
- Measurement tools, metric/imperial display, layout bounds, and occupied-grid counts.
- Scene hierarchy, named groups, visibility, locking, layers, and floor heights.
- OpenForge display-name rules and manual overrides.
- Screenshot, STL, OBJ, and complete project-package exports.

## Performance and release hardening

- Improve reduced-detail viewport quality while retaining responsive input.
- Validate 1,000 indexed STLs, 500 placed pieces, and 100 repeated instances.
- Add cancellation and progress throughout scans, thumbnails, imports, and exports.
- Add a local error-log window, accessibility review, installer, and signed release process.
