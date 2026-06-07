# Annotated Frames Stay Train State Focused

Type: DONE  
Labels: done

## Parent

docs/prd/vision-pipeline-visual-observation-integration-prd.md

## What to build

Keep Annotated Frames focused on Train State while Moving Object Observations and Train Observations remain Debug Frame evidence unless Train Tracking turns them into Train State.

## Acceptance criteria

- [x] Annotated Frames draw Train State by default.
- [x] Moving Object Observations and Train Observations are not drawn on Annotated Frames unless represented as Train State.
- [x] Debug Frames remain the operator-facing path for visual evidence phases.
- [x] Tests prove observation-only snapshots do not annotate the normal tile as if Train State exists.

## Blocked by

- .scratch/issues/0057-debug-frames-show-visual-evidence-phases.md
