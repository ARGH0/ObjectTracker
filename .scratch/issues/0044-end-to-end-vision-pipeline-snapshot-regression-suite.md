# End-To-End Vision Pipeline Snapshot Regression Suite

Type: AFK  
Labels: done

## Parent

.scratch/issues/0030-vision-pipeline-snapshot-frame-feed-prd.md

## What to build

Add integration-style regression coverage for the full operator flow from Camera Source setup through Vision Pipeline start, snapshot rendering, Debug View, status overlays, target FPS, Train Tracking continuity, Ambiguity Alert behavior, and stop.

## Acceptance criteria

- [x] Tests cover Camera Source setup for USB and file-based sources.
- [x] Tests cover visible/included, visible/excluded, hidden/included, and hidden/excluded behavior.
- [x] Tests cover Vision Pipeline start, first snapshot placeholder, snapshot display, Debug View toggle, inclusion change, and stop.
- [x] Tests cover Camera Source Status and Vision Pipeline Lane Status projections.
- [x] Tests cover global target FPS and actual per-lane processed FPS behavior.
- [x] Tests cover Train Tracking Local Train ID continuity and handoff ambiguity behavior.

## Blocked by

- .scratch/issues/0039-ui-tile-routing-from-pipelinesnapshot.md
- .scratch/issues/0040-ui-missing-frame-and-status-overlay-behavior.md
- .scratch/issues/0041-visual-observation-snapshot-adapter.md
- .scratch/issues/0042-train-tracking-internal-module-baseline.md
- .scratch/issues/0043-handoff-grace-and-ambiguity-alert-integration.md
