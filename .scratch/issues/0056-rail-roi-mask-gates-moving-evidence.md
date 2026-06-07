# Rail ROI Mask Gates Moving Evidence

Type: AFK  
Labels: ready-for-agent

## Parent

docs/prd/vision-pipeline-visual-observation-integration-prd.md

## What to build

Apply Rail ROI masks before moving object extraction in the Vision Pipeline visual observation path. Motion outside Rail ROI should not emit Moving Object Observations or Train Observations.

## Acceptance criteria

- [ ] Visual observation applies Rail ROI gating before contour extraction.
- [ ] Motion inside Rail ROI can emit Moving Object Observations.
- [ ] Motion outside Rail ROI emits no Moving Object Observations or Train Observations.
- [ ] Tests prove Rail ROI gating affects PipelineSnapshot observation output.

## Blocked by

- .scratch/issues/0054-file-camera-source-background-feeds-visual-observation.md
- .scratch/issues/0055-usb-camera-source-background-feeds-visual-observation.md
