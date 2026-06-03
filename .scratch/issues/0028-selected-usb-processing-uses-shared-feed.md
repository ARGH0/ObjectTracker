# Selected USB Processing Uses Shared Feed

Type: AFK  
Labels: ready-for-agent

## Parent

.scratch/issues/0021-session-owned-usb-camera-source-feeds-prd.md

## What to build

Route selected USB camera processing, calibration, and background sampling through the shared USB Camera Source owner where touched. The Vision Pipeline remains single-selected-camera scoped, but it should not open a competing USB capture handle for the same physical Camera Source.

## Acceptance criteria

- [ ] Selected USB processing acquires a shared owner lease instead of directly opening a duplicate USB capture handle.
- [ ] USB calibration/background sampling consumes frames from the shared owner snapshot path.
- [ ] Processing waits for fresh frame versions where possible and avoids repeatedly processing the same frame in a tight loop.
- [ ] Frame snapshots are decoded for processing in the first iteration; raw mutable frame sharing is not introduced.
- [ ] Hiding a tile does not stop a USB owner while selected-camera processing still holds a consumer lease.
- [ ] This slice does not implement simultaneous Train State processing across multiple Camera Zones.
- [ ] Tests cover no duplicate capture open during selected USB processing, calibration/shared-owner use, frame-version freshness, and hidden-but-included owner retention.

## Blocked by

- .scratch/issues/0022-session-owned-usb-feed-owner-baseline.md
- .scratch/issues/0025-usb-source-runtime-status-and-restart-action.md
