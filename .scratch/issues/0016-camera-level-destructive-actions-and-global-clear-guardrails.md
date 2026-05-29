# Camera-Level Destructive Actions And Global Clear Guardrails

Type: AFK  
Labels: ready-for-agent

## Parent

- `.scratch/issues/0010-main-window-workspace-and-vision-pipeline-prd.md`

## What to build

Implement destructive action guardrails so camera delete is a camera-level action in the Camera Panel with explicit confirmation, and `Clear Cameras` is only allowed when Vision Pipeline is stopped with a clear destructive confirmation flow.

## Acceptance criteria

- [ ] Camera delete action exists in camera list context and requires explicit confirmation.
- [ ] `Clear Cameras` is unavailable while Vision Pipeline is running.
- [ ] `Clear Cameras` requires explicit destructive confirmation before execution.

## Blocked by

- `.scratch/issues/0012-vision-pipeline-dropdown-control-with-runtime-action-validity.md`
- `.scratch/issues/0013-camera-panel-states-and-single-select-camera-context.md`
