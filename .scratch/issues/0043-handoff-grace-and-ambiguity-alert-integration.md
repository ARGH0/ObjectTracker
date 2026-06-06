# Handoff Grace And Ambiguity Alert Integration

Type: AFK  
Labels: ready-for-agent

## Parent

.scratch/issues/0030-vision-pipeline-snapshot-frame-feed-prd.md

## What to build

Add global handoff grace handling for Local Train IDs appearing across Camera Sources, and raise an Ambiguity Alert when duplicate presence exceeds the allowed handoff window.

## Acceptance criteria

- [ ] Same Local Train ID may appear in multiple Camera Source snapshots during the global handoff grace period.
- [ ] Duplicate Local Train ID presence beyond the grace period raises an Ambiguity Alert through the separate safety/intervention flow.
- [ ] Ambiguity Alert state is not embedded in PipelineSnapshot.
- [ ] Other Camera Source lanes continue running when an Ambiguity Alert is raised.
- [ ] Tests cover allowed handoff and beyond-grace ambiguity behavior.

## Blocked by

- .scratch/issues/0042-train-tracking-internal-module-baseline.md
