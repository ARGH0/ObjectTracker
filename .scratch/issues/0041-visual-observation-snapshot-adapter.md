# Visual Observation Snapshot Adapter

Type: AFK  
Labels: ready-for-agent

## Parent

.scratch/issues/0030-vision-pipeline-snapshot-frame-feed-prd.md

## What to build

Adapt current Calibration, Rail ROI, moving object, Train Color, annotation, and Debug Frame production into the new PipelineSnapshot flow without requiring a full vision rewrite.

## Acceptance criteria

- [ ] Current visual processing can emit Source Frame and Annotated Frame through PipelineSnapshot.
- [ ] Moving Object Observations and Train Observations are represented separately from Train State.
- [ ] Annotated Frame draws Train State by default, not raw observations.
- [ ] Debug Frames expose visual observation phases when Debug View is enabled.
- [ ] Existing Calibration and Rail ROI behavior is preserved through the adapter-first path.
- [ ] Tests verify snapshot content without relying on UI callbacks.

## Blocked by

- .scratch/issues/0035-pipeline-snapshot-shape-and-recording-output-adapter.md
- .scratch/issues/0036-debug-frames-replace-previewframeset.md
- .scratch/issues/0037-multi-lane-vision-pipeline-reconciliation.md
