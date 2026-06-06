# Debug Frames Replace PreviewFrameSet

Type: AFK  
Labels: done

## Parent

.scratch/issues/0030-vision-pipeline-snapshot-frame-feed-prd.md

## What to build

Move Debug View output to named Debug Frames on PipelineSnapshot. Debug Frames should be produced only when Debug View is enabled and should replace PreviewFrameSet as the UI-facing debug output concept.

## Acceptance criteria

- [x] PipelineSnapshot can carry named Debug Frames.
- [x] Debug Frames are emitted only when Debug View is enabled for the Camera Source.
- [x] Debug View can render from Debug Frames rather than PreviewFrameSet.
- [x] PreviewFrameSet does not cross the Vision Pipeline-to-UI seam.
- [x] Tests cover Debug View enabled and disabled snapshot output.

## Blocked by

- .scratch/issues/0035-pipeline-snapshot-shape-and-recording-output-adapter.md
