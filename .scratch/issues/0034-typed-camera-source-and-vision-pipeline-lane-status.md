# Typed Camera Source And Vision Pipeline Lane Status

Type: AFK  
Labels: done

## Parent

.scratch/issues/0030-vision-pipeline-snapshot-frame-feed-prd.md

## What to build

Introduce distinct typed status flows for Camera Source Status and Vision Pipeline Lane Status. The UI should be able to show feed state separately from per-Camera Source processing state in tiles and the Camera Panel.

## Acceptance criteria

- [x] Camera Source Status and Vision Pipeline Lane Status are distinct typed values.
- [x] Both status types use starting, running, stale, failed, and stopped states.
- [x] Camera Panel projection can show both statuses for the selected Camera Source.
- [x] Visible tile projection can show compact status without mixing feed and lane state.
- [x] Tests assert typed status values without parsing free-form runtime log text.

## Blocked by

None - can start immediately
