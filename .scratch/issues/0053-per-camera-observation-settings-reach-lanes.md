# Per-Camera Observation Settings Reach Lanes

Type: AFK  
Labels: done

## Parent

docs/prd/vision-pipeline-visual-observation-integration-prd.md

## What to build

Pass each Camera Source's observation settings into its Vision Pipeline lane. Threshold, Motion Area, Color Min Pixels, Morph Kernel Size, Process Max Width, and Train Color calibrations should reach the visual observation module according to the existing pending-restart apply policy.

## Acceptance criteria

- [x] Each included Camera Source lane receives its own observation settings.
- [x] Settings saved in the Camera workspace are reflected in lane configuration after the appropriate apply/restart path.
- [x] Processing-critical changes still mark pending Vision Pipeline restart while running.
- [x] Tests prove two Camera Sources can use different observation settings.

## Blocked by

- .scratch/issues/0048-static-color-blobs-are-not-train-observations.md
