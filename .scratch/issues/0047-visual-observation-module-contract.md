# Visual Observation Module Contract

Type: AFK  
Labels: done

## Parent

docs/prd/vision-pipeline-visual-observation-integration-prd.md

## What to build

Create a Vision-owned visual observation seam that can accept a Source Frame plus observation settings and return Moving Object Observations, Train Observations, and Debug Frames. The slice is complete when the Vision Pipeline can depend on this seam in tests without pulling in UI-owned background estimation code or Avalonia dependencies.

## Acceptance criteria

- [x] A Vision-owned visual observation interface or service exists and has no UI assembly dependency.
- [x] The seam accepts a Source Frame and observation settings needed for later Rail ROI, motion, Train Color, and Debug Frame behavior.
- [x] The seam returns Moving Object Observations, Train Observations, and Debug Frames in domain terms.
- [x] Tests can use a fake visual observation implementation to drive PipelineSnapshot behavior.

## Blocked by

None - can start immediately
