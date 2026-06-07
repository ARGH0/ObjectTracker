# Annotated Frames Stay Train State Focused

Type: AFK  
Labels: ready-for-agent

## Parent

docs/prd/vision-pipeline-visual-observation-integration-prd.md

## What to build

Keep Annotated Frames focused on Train State while Moving Object Observations and Train Observations remain Debug Frame evidence unless Train Tracking turns them into Train State.

## Acceptance criteria

- [ ] Annotated Frames draw Train State by default.
- [ ] Moving Object Observations and Train Observations are not drawn on Annotated Frames unless represented as Train State.
- [ ] Debug Frames remain the operator-facing path for visual evidence phases.
- [ ] Tests prove observation-only snapshots do not annotate the normal tile as if Train State exists.

## Blocked by

- .scratch/issues/0057-debug-frames-show-visual-evidence-phases.md
