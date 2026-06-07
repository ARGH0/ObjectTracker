# Handoff Grace And Ambiguity Alert Integration

Type: AFK  
Labels: done

## Parent

.scratch/issues/0030-vision-pipeline-snapshot-frame-feed-prd.md

## What to build

Add global handoff grace handling for Local Train IDs appearing across Camera Sources, and raise an Ambiguity Alert when duplicate presence exceeds the allowed handoff window.

## Acceptance criteria

- [x] Same Local Train ID may appear in multiple Camera Source snapshots during the global handoff grace period.
- [x] Duplicate Local Train ID presence beyond the grace period raises an Ambiguity Alert through the separate safety/intervention flow.
- [x] Ambiguity Alert state is not embedded in PipelineSnapshot.
- [x] Other Camera Source lanes continue running when an Ambiguity Alert is raised.
- [x] Tests cover allowed handoff and beyond-grace ambiguity behavior.

## Blocked by

- .scratch/issues/0042-train-tracking-internal-module-baseline.md
