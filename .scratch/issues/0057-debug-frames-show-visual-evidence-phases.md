# Debug Frames Show Visual Evidence Phases

Type: AFK  
Labels: ready-for-agent

## Parent

docs/prd/vision-pipeline-visual-observation-integration-prd.md

## What to build

Emit Debug Frames that show the actual visual evidence phases used by the Vision Pipeline: Source Frame, Rail ROI mask, motion mask, moving object evidence, Train Color evidence, and Train Tracking explanation where available.

## Acceptance criteria

- [ ] Debug Frames are emitted through PipelineSnapshot only when Debug View is enabled for the Camera Source.
- [ ] Debug Frames include visual evidence phases rather than copied placeholder Source Frames.
- [ ] Debug View routes the current Debug Frame names without dropping visual evidence.
- [ ] Tests prove PipelineSnapshot Debug Frames explain Moving Object Observations and Train Observations.

## Blocked by

- .scratch/issues/0050-motion-gated-train-color-classification.md
- .scratch/issues/0051-morph-kernel-size-refines-moving-evidence.md
- .scratch/issues/0056-rail-roi-mask-gates-moving-evidence.md
