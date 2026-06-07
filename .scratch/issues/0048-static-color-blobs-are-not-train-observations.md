# Static Color Blobs Are Not Train Observations

Type: AFK  
Labels: done

## Parent

docs/prd/vision-pipeline-visual-observation-integration-prd.md

## What to build

Wire the Vision Pipeline lane through the visual observation seam so Train Observations come from visual observation output rather than direct full-frame Train Color contour detection. The completed slice should prove static full-frame Train Color blobs are not emitted as Train Observations by the default lane path.

## Acceptance criteria

- [x] The Vision Pipeline lane can publish PipelineSnapshots from visual observation seam output.
- [x] A regression test proves a static Train Color blob outside moving evidence does not become a Train Observation.
- [x] Train Tracking still receives Train Observations as evidence and remains responsible for Train State.
- [x] Existing PipelineSnapshot output behavior remains compatible with UI routing tests.

## Blocked by

- .scratch/issues/0047-visual-observation-module-contract.md
