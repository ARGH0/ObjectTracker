# Default Runtime Uses Visual Observation Path

Type: AFK  
Labels: ready-for-agent

## Parent

docs/prd/vision-pipeline-visual-observation-integration-prd.md

## What to build

Make the visual observation module the default observation source for USB and file Camera Sources in the running Vision Pipeline. Direct full-frame Train Color detection should no longer be the default runtime path for normal Train Observations.

## Acceptance criteria

- [ ] USB and file Camera Sources use the visual observation module in normal Vision Pipeline runtime.
- [ ] Direct full-frame Train Color detection is removed, demoted, or explicitly isolated from default runtime observation behavior.
- [ ] PipelineSnapshots still contain Source Frame, Annotated Frame, observations, Train States, Debug Frames, and timing metadata.
- [ ] Regression tests prove the observed full-frame color blob explosion does not return.

## Blocked by

- .scratch/issues/0050-motion-gated-train-color-classification.md
- .scratch/issues/0053-per-camera-observation-settings-reach-lanes.md
- .scratch/issues/0054-file-camera-source-background-feeds-visual-observation.md
- .scratch/issues/0055-usb-camera-source-background-feeds-visual-observation.md
- .scratch/issues/0056-rail-roi-mask-gates-moving-evidence.md
- .scratch/issues/0057-debug-frames-show-visual-evidence-phases.md
- .scratch/issues/0058-annotated-frames-stay-train-state-focused.md
