# Camera Panel States And Single-Select Camera Context

Type: AFK  
Labels: ready-for-agent

## Parent

- `.scratch/issues/0010-main-window-workspace-and-vision-pipeline-prd.md`

## What to build

Implement the left `Camera Panel` in `overlay` and `pinned` states, with remembered pin state when returning to `Camera` workspace. The panel must provide a single-select camera list whose selection defines camera-specific configuration context.

## Acceptance criteria

- [ ] Camera Panel supports `overlay` and `pinned` behavior with expected layout effects.
- [ ] Pin/unpin state is remembered when returning to `Camera` workspace.
- [ ] Camera list is single-select and selected camera drives camera-specific configuration context.

## Blocked by

- `.scratch/issues/0011-main-window-shell-with-global-workspaces-and-persistent-status-bar.md`
