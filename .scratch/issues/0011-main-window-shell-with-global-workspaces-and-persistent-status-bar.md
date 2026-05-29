# Main Window Shell With Global Workspaces And Persistent Status Bar

Type: AFK  
Labels: ready-for-agent

## Parent

- `.scratch/issues/0010-main-window-workspace-and-vision-pipeline-prd.md`

## What to build

Implement the main window shell so operators can switch between `Camera`, `Layers`, and `Settings` in the center workspace while keeping a persistent top menu bar and an always-visible, indicator-only bottom status bar. The bottom bar must show Vision Pipeline runtime state, Ambiguity Alert state, Calibration state, and pending restart state across all workspaces.

## Acceptance criteria

- [ ] Top menu includes global workspace entries `Camera`, `Layers`, and `Settings`, and switching entries replaces center workspace content.
- [ ] Bottom status bar remains visible in all workspaces and displays runtime indicators as read-only status.
- [ ] Workspace navigation remains available during Ambiguity Alert without forced redirects.

## Blocked by

None - can start immediately.
