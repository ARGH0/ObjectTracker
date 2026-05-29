# Settings Apply Policy Tags And Pending Restart Signaling

Type: AFK  
Labels: ready-for-agent

## Parent

- `.scratch/issues/0010-main-window-workspace-and-vision-pipeline-prd.md`

## What to build

Implement per-setting inline apply policy tags (`applies immediately` vs `requires Vision Pipeline restart`) and end-to-end pending restart signaling in Settings and bottom status bar when saved restart-required changes are not yet active.

## Acceptance criteria

- [ ] Settings controls display inline apply policy tags.
- [ ] Saving restart-required settings marks pending restart until Vision Pipeline stop/start cycle completes.
- [ ] Pending restart state is visible in both Settings workspace and bottom status bar.

## Blocked by

- `.scratch/issues/0012-vision-pipeline-dropdown-control-with-runtime-action-validity.md`
- `.scratch/issues/0018-settings-workspace-draft-save-model-and-unsaved-change-navigation-guard.md`
