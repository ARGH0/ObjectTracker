# Global Layer Type Workspace With Usage Visibility And Delete Blocking

Type: AFK  
Labels: ready-for-agent

## Parent

- `.scratch/issues/0010-main-window-workspace-and-vision-pipeline-prd.md`

## What to build

Implement `Layers` workspace ownership for global `Layer Type` management and expose read-only camera usage for each Layer Type. Deletion of a Layer Type must be blocked when any camera still uses it through Camera Layer Regions.

## Acceptance criteria

- [ ] Layers workspace manages global Layer Type lifecycle.
- [ ] Selected Layer Type shows read-only camera usage information.
- [ ] Deleting an in-use Layer Type is blocked with clear dependency context.

## Blocked by

- `.scratch/issues/0011-main-window-shell-with-global-workspaces-and-persistent-status-bar.md`
- `.scratch/issues/0013-camera-panel-states-and-single-select-camera-context.md`
