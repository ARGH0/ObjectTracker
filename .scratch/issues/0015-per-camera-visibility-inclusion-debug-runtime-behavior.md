# Per-Camera Visibility Inclusion Debug Runtime Behavior

Type: AFK  
Labels: ready-for-agent

## Parent

- `.scratch/issues/0010-main-window-workspace-and-vision-pipeline-prd.md`

## What to build

Implement per-camera immediate controls for `Camera Visibility`, `Vision Pipeline Inclusion`, and `Debug View`. Keep `Vision Pipeline Inclusion` independent from visibility, remove hidden cameras from grid, render excluded-but-visible cameras as raw feed without Train State annotations, and retain debug mode state within the current Session.

## Acceptance criteria

- [ ] Camera visibility/inclusion/debug toggles apply immediately per camera.
- [ ] Hidden cameras are removed from camera grid; excluded-but-visible cameras show raw feed only with no Train State annotations.
- [ ] Per-camera debug mode state is retained within the active Session, including while hidden.

## Blocked by

- `.scratch/issues/0013-camera-panel-states-and-single-select-camera-context.md`
- `.scratch/issues/0014-camera-workspace-reflow-and-manual-tile-ordering.md`
