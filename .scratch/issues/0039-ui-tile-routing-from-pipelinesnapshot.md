# UI Tile Routing From PipelineSnapshot

Type: AFK  
Labels: ready-for-agent

## Parent

.scratch/issues/0030-vision-pipeline-snapshot-frame-feed-prd.md

## What to build

Route visible included Camera Source tiles through PipelineSnapshot while the Vision Pipeline is running. Visible excluded tiles and visible tiles while the Vision Pipeline is stopped should use raw Camera Source feed outside the Vision Pipeline.

## Acceptance criteria

- [ ] Visible included running tiles display Annotated Frame or Debug Frames from PipelineSnapshot.
- [ ] Visible included stopped tiles display raw Camera Source feed with stopped state available separately.
- [ ] Visible excluded tiles display raw Camera Source feed and do not require PipelineSnapshot.
- [ ] Hidden included Camera Sources have no tile but can still be processed.
- [ ] Starting the Vision Pipeline switches included visible tiles to snapshot mode with a starting placeholder until first snapshot.
- [ ] Excluding a visible Camera Source switches the tile immediately to raw feed mode.
- [ ] Tests cover all Camera Visibility, Vision Pipeline Inclusion, running/stopped, and Debug View routing combinations.

## Blocked by

- .scratch/issues/0033-unified-camera-source-feed-consumption.md
- .scratch/issues/0035-pipeline-snapshot-shape-and-recording-output-adapter.md
- .scratch/issues/0036-debug-frames-replace-previewframeset.md
- .scratch/issues/0037-multi-lane-vision-pipeline-reconciliation.md
