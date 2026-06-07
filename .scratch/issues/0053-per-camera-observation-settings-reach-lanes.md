# Per-Camera Observation Settings Reach Lanes

Type: AFK  
Labels: ready-for-agent

## Parent

docs/prd/vision-pipeline-visual-observation-integration-prd.md

## What to build

Pass each Camera Source's observation settings into its Vision Pipeline lane. Threshold, Motion Area, Color Min Pixels, Morph Kernel Size, Process Max Width, and Train Color calibrations should reach the visual observation module according to the existing pending-restart apply policy.

## Acceptance criteria

- [ ] Each included Camera Source lane receives its own observation settings.
- [ ] Settings saved in the Camera workspace are reflected in lane configuration after the appropriate apply/restart path.
- [ ] Processing-critical changes still mark pending Vision Pipeline restart while running.
- [ ] Tests prove two Camera Sources can use different observation settings.

## Blocked by

- .scratch/issues/0048-static-color-blobs-are-not-train-observations.md
