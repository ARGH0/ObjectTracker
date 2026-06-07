# Motion-Gated Train Color Classification

Type: AFK  
Labels: ready-for-agent

## Parent

docs/prd/vision-pipeline-visual-observation-integration-prd.md

## What to build

Classify Train Color only inside Moving Object Observation regions. A moving region with enough matching Train Color pixels should emit a Train Observation, while static Train Color outside moving evidence should not.

## Acceptance criteria

- [ ] Train Color classification runs inside moving evidence regions rather than across the full Source Frame.
- [ ] Color Min Pixels filters weak color speckles before they become Train Observations.
- [ ] Saved Train Color calibration ranges are used by the visual observation module.
- [ ] Tests prove moving color evidence emits a Train Observation and static color evidence does not.

## Blocked by

- .scratch/issues/0049-foreground-motion-produces-moving-object-observations.md
