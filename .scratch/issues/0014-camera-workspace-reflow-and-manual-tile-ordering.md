# Camera Workspace Reflow And Manual Tile Ordering

Type: AFK  
Labels: ready-for-agent

## Parent

- `.scratch/issues/0010-main-window-workspace-and-vision-pipeline-prd.md`

## What to build

Implement camera grid composition in `Camera` workspace so layout reflows by visible camera count and tile order follows manually managed camera list order. Reordering is presentation-only and must not affect Camera Zone identity or Vision Pipeline behavior.

## Acceptance criteria

- [ ] Camera grid reflows based on visible camera count.
- [ ] Camera tile order follows manual order from camera list.
- [ ] Reordering does not modify Camera Zone bindings or Vision Pipeline processing behavior.

## Blocked by

- `.scratch/issues/0013-camera-panel-states-and-single-select-camera-context.md`
