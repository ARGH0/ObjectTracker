# Remove Deprecated Frame And Processing Paths

Type: AFK  
Labels: ready-for-agent

## Parent

.scratch/issues/0030-vision-pipeline-snapshot-frame-feed-prd.md

## What to build

Remove obsolete and transitional code after the PipelineSnapshot flow is covered by regression tests. The codebase should no longer keep deprecated frame or processing paths that duplicate the agreed Vision Pipeline flow.

## Acceptance criteria

- [ ] No public UI frame seam uses PreviewFrameSet.
- [ ] No visible included tile uses a separate raw preview consumer while the Vision Pipeline is running.
- [ ] No playlist runtime path remains for file-based Camera Sources.
- [ ] No selected-Camera Source loop controls Vision Pipeline processing.
- [ ] Transitional adapters that are no longer needed after migration are removed.
- [ ] End-to-end Vision Pipeline snapshot regression tests still pass after cleanup.

## Blocked by

- .scratch/issues/0039-ui-tile-routing-from-pipelinesnapshot.md
- .scratch/issues/0041-visual-observation-snapshot-adapter.md
- .scratch/issues/0044-end-to-end-vision-pipeline-snapshot-regression-suite.md
