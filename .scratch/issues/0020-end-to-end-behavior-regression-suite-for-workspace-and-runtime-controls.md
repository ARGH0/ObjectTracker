# End-To-End Behavior Regression Suite For Workspace And Runtime Controls

Type: AFK  
Labels: ready-for-agent

## Parent

- `.scratch/issues/0010-main-window-workspace-and-vision-pipeline-prd.md`

## What to build

Add regression coverage that verifies end-to-end operator behavior for workspace switching, runtime control validity, camera visibility/inclusion semantics, destructive action guardrails, layer deletion blocking, and settings pending restart behavior.

## Acceptance criteria

- [ ] Tests cover workspace shell and runtime control behavior under normal and edge-case states.
- [ ] Tests cover per-camera visibility/inclusion/debug semantics and destructive action guardrails.
- [ ] Tests cover Layer Type usage delete blocking and settings pending restart behavior.

## Blocked by

- `.scratch/issues/0011-main-window-shell-with-global-workspaces-and-persistent-status-bar.md`
- `.scratch/issues/0012-vision-pipeline-dropdown-control-with-runtime-action-validity.md`
- `.scratch/issues/0015-per-camera-visibility-inclusion-debug-runtime-behavior.md`
- `.scratch/issues/0016-camera-level-destructive-actions-and-global-clear-guardrails.md`
- `.scratch/issues/0017-global-layer-type-workspace-with-usage-visibility-and-delete-blocking.md`
- `.scratch/issues/0019-settings-apply-policy-tags-and-pending-restart-signaling.md`
