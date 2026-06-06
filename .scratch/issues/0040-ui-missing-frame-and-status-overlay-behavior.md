# UI Missing-Frame And Status Overlay Behavior

Type: AFK  
Labels: ready-for-agent

## Parent

.scratch/issues/0030-vision-pipeline-snapshot-frame-feed-prd.md

## What to build

Add global UI missing-frame behavior for tile display with status overlays. Operators can choose repeat-last-frame or black-frame, and stale or failed status must remain visible either way.

## Acceptance criteria

- [ ] UI missing-frame behavior supports repeat-last-frame and black-frame.
- [ ] repeat-last-frame is the default.
- [ ] Stale and failed overlays appear regardless of missing-frame behavior.
- [ ] No-frame-yet state shows a placeholder or black frame rather than stale content.
- [ ] Behavior works for Annotated Frame, Debug Frames, and raw Camera Source feed display.
- [ ] Tests cover stale, failed, no-frame-yet, repeat-last-frame, and black-frame scenarios.

## Blocked by

- .scratch/issues/0034-typed-camera-source-and-vision-pipeline-lane-status.md
- .scratch/issues/0039-ui-tile-routing-from-pipelinesnapshot.md
