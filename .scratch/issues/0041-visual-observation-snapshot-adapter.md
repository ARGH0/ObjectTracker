# Visual Observation Snapshot Adapter

Type: AFK  
Labels: done

## Parent

.scratch/issues/0030-vision-pipeline-snapshot-frame-feed-prd.md

## What to build

Adapt current Calibration, Rail ROI, moving object, Train Color, annotation, and Debug Frame production into the new PipelineSnapshot flow without requiring a full vision rewrite.

## Acceptance criteria

- [x] Current visual processing can emit Source Frame and Annotated Frame through PipelineSnapshot.
- [x] Moving Object Observations and Train Observations are represented separately from Train State.
- [x] Annotated Frame draws Train State by default, not raw observations.
- [x] Debug Frames expose visual observation phases when Debug View is enabled.
- [x] Existing Calibration and Rail ROI behavior is preserved through the adapter-first path.
- [x] Tests verify snapshot content without relying on UI callbacks.

## Blocked by

- .scratch/issues/0035-pipeline-snapshot-shape-and-recording-output-adapter.md
- .scratch/issues/0036-debug-frames-replace-previewframeset.md
- .scratch/issues/0037-multi-lane-vision-pipeline-reconciliation.md
