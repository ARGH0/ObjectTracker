# Vision Pipeline Dropdown Control With Runtime Action Validity

Type: AFK  
Labels: ready-for-agent

## Parent

- `.scratch/issues/0010-main-window-workspace-and-vision-pipeline-prd.md`

## What to build

Implement a top-bar `Vision Pipeline` dropdown that always shows both `Start` and `Stop` actions, while disabling the invalid action for current runtime state. Include pending-restart awareness in the runtime control/status model so operators can see when saved settings are not yet active.

## Acceptance criteria

- [ ] `Vision Pipeline` dropdown shows both `Start` and `Stop` actions at all times.
- [ ] Invalid action is disabled with clear state hinting based on current runtime state.
- [ ] Pending restart state is represented in runtime control/status behavior.

## Blocked by

- `.scratch/issues/0011-main-window-shell-with-global-workspaces-and-persistent-status-bar.md`
