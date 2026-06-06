# Typed Camera Source And Vision Pipeline Lane Status

Type: AFK  
Labels: ready-for-agent

## Parent

.scratch/issues/0030-vision-pipeline-snapshot-frame-feed-prd.md

## What to build

Introduce distinct typed status flows for Camera Source Status and Vision Pipeline Lane Status. The UI should be able to show feed state separately from per-Camera Source processing state in tiles and the Camera Panel.

## Acceptance criteria

- [ ] Camera Source Status and Vision Pipeline Lane Status are distinct typed values.
- [ ] Both status types use starting, running, stale, failed, and stopped states.
- [ ] Camera Panel projection can show both statuses for the selected Camera Source.
- [ ] Visible tile projection can show compact status without mixing feed and lane state.
- [ ] Tests assert typed status values without parsing free-form runtime log text.

## Blocked by

None - can start immediately
