# Settings Workspace Draft Save Model And Unsaved-Change Navigation Guard

Type: AFK  
Labels: ready-for-agent

## Parent

- `.scratch/issues/0010-main-window-workspace-and-vision-pipeline-prd.md`

## What to build

Implement `Settings` workspace explicit save flow with draft state and unsaved-change guarding on navigation. On leaving Settings with pending edits, operators must get `Save / Discard / Cancel` and the chosen action must be applied consistently.

## Acceptance criteria

- [ ] Settings edits remain draft until explicitly saved.
- [ ] Navigation with unsaved settings prompts `Save / Discard / Cancel`.
- [ ] Prompt choice consistently applies save, discard, or cancel behavior.

## Blocked by

- `.scratch/issues/0011-main-window-shell-with-global-workspaces-and-persistent-status-bar.md`
