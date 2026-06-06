# Global Vision Pipeline Target FPS

Type: AFK  
Labels: done

## Parent

.scratch/issues/0030-vision-pipeline-snapshot-frame-feed-prd.md

## What to build

Add one global Vision Pipeline target FPS setting that applies immediately to every running lane. Each lane should process the latest available Source Frame at the target cadence and report actual processed FPS separately.

## Acceptance criteria

- [x] One global target FPS setting is available for the Vision Pipeline.
- [x] Changing target FPS while running updates all lanes without restart or Train State reset.
- [x] Lanes process latest available Source Frames rather than a backlog.
- [x] PipelineSnapshot or status output reports actual processed FPS per lane separately from configured target FPS.
- [x] Tests cover immediate target FPS changes and latest-frame processing behavior.

## Blocked by

- .scratch/issues/0037-multi-lane-vision-pipeline-reconciliation.md
