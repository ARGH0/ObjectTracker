# Main window workspace and Vision Pipeline behavior

The main window uses persistent top and bottom bars with workspace switching in the center: top global menus are `Camera`, `Layers`, `Settings`, and `Vision Pipeline`, while the bottom status bar is always visible and indicator-only for runtime state (`Vision Pipeline` running/stopped, Calibration, and pending restart). We chose this over a camera-always-visible layout and clickable global alert controls because operators need clear global runtime awareness while still being free to navigate between workspaces without forced detours. `Camera` workspace reflows by visible cameras and is driven by a left `Camera Panel` (`overlay` or `pinned`) with single-select camera configuration; `Camera Visibility`, `Vision Pipeline Inclusion`, and per-camera `Debug View` apply immediately, `Vision Pipeline Inclusion` is independent from visibility, hidden cameras are removed from the grid, excluded-but-visible cameras show raw feed without annotations, and `Debug View` state is retained within the current Session. `Layers` workspace owns global Layer Types only and shows read-only camera usage with deletion blocked when in use, while `Settings` uses explicit save with unsaved-change confirmation and inline per-setting apply tags so processing-critical changes can be saved as pending and applied only after stopping and starting the Vision Pipeline.

## Considered Options

- Keep camera feeds always visible while editing Layers/Settings.
- Make global alert indicators clickable with forced navigation to resolution controls.
- Couple camera visibility and processing inclusion into one toggle.
- Allow destructive global/layer actions without strict runtime and usage guards.
