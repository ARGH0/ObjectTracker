# File Camera Source Background Feeds Visual Observation

Type: AFK  
Labels: ready-for-agent

## Parent

docs/prd/vision-pipeline-visual-observation-integration-prd.md

## What to build

Use file-based Calibration/background state as Vision-owned lane input so file Camera Sources use background-diff visual observation during the running Vision Pipeline. The file-based path should no longer rely on UI-owned background estimation runtime logic.

## Acceptance criteria

- [ ] File Camera Source lanes can access background state needed for foreground extraction.
- [ ] File-based visual observation uses background-diff behavior during Vision Pipeline runtime.
- [ ] Existing file Camera Source feed ownership and loop behavior remain intact.
- [ ] Tests prove file Camera Source motion relative to background emits Moving Object Observations.

## Blocked by

- .scratch/issues/0049-foreground-motion-produces-moving-object-observations.md
- .scratch/issues/0053-per-camera-observation-settings-reach-lanes.md
