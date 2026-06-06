# Multi-Lane Vision Pipeline Reconciliation

Type: AFK  
Labels: done

## Parent

.scratch/issues/0030-vision-pipeline-snapshot-frame-feed-prd.md

## What to build

Replace selected-Camera Source processing with one Vision Pipeline runtime that reconciles active processing lanes to desired Vision Pipeline Inclusion and lane configuration.

## Acceptance criteria

- [x] Starting the Vision Pipeline starts lanes for all included Camera Sources.
- [x] Stopping the Vision Pipeline stops all active lanes.
- [x] Changing Vision Pipeline Inclusion while running starts or stops only the affected lane.
- [x] Debug View changes apply immediately to the affected lane.
- [x] A failed or stale lane does not stop other lanes.
- [x] Tests cover start, stop, include, exclude, and immediate Debug View changes.

## Blocked by

- .scratch/issues/0034-typed-camera-source-and-vision-pipeline-lane-status.md
- .scratch/issues/0035-pipeline-snapshot-shape-and-recording-output-adapter.md
