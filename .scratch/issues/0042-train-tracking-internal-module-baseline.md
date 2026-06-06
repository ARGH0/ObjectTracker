# Train Tracking Internal Module Baseline

Type: AFK  
Labels: ready-for-agent

## Parent

.scratch/issues/0030-vision-pipeline-snapshot-frame-feed-prd.md

## What to build

Introduce Train Tracking as a distinct internal module under the Vision Pipeline runtime. It should turn observations from Camera Sources into Train State with stable Local Train IDs inside one Processing Unit.

## Acceptance criteria

- [ ] Train Tracking is distinct from per-Camera Source visual observation lanes.
- [ ] Local Train IDs are shared across Camera Sources inside one Processing Unit.
- [ ] Relevant Train States appear in per-Camera Source PipelineSnapshots.
- [ ] Train State can continue across frames when no new observation appears.
- [ ] Tests cover Local Train ID continuity across at least two Camera Sources.

## Blocked by

- .scratch/issues/0035-pipeline-snapshot-shape-and-recording-output-adapter.md
- .scratch/issues/0037-multi-lane-vision-pipeline-reconciliation.md
- .scratch/issues/0041-visual-observation-snapshot-adapter.md
