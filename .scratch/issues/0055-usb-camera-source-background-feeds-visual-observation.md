# USB Camera Source Background Feeds Visual Observation

Type: DONE  
Labels: done

## Parent

docs/prd/vision-pipeline-visual-observation-integration-prd.md

## What to build

Use session-owned USB Camera Source background sampling and calibration state as Vision-owned lane input so USB Camera Sources use background-diff visual observation during the running Vision Pipeline without competing camera handles.

## Acceptance criteria

- [x] USB Camera Source lanes can access background state needed for foreground extraction through session-owned feed behavior.
- [x] USB visual observation uses background-diff behavior during Vision Pipeline runtime.
- [x] Starting visual observation does not open a competing USB camera handle.
- [x] Tests prove USB Camera Source motion relative to background emits Moving Object Observations through the shared feed path.

## Blocked by

- .scratch/issues/0049-foreground-motion-produces-moving-object-observations.md
- .scratch/issues/0053-per-camera-observation-settings-reach-lanes.md
