# Process Width Defines Observation Coordinate Space

Type: AFK  
Labels: done

## Parent

docs/prd/vision-pipeline-visual-observation-integration-prd.md

## What to build

Apply Process Max Width consistently so Source Frame processing, Moving Object Observations, Train Observations, Train State annotations, and Debug Frames share the Vision Pipeline coordinate space.

## Acceptance criteria

- [x] Process Max Width controls visual observation processing size without breaking frame routing.
- [x] Observation coordinates are in the same coordinate space as Annotated Frames and Debug Frames.
- [x] Tests prove resized processing still emits correctly positioned observations.
- [x] Existing Annotated Frame rendering remains aligned with Train State positions.

## Blocked by

- .scratch/issues/0049-foreground-motion-produces-moving-object-observations.md
