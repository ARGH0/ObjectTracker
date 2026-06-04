# Selected USB Processing Uses Shared Feed

Type: AFK  
Labels: completed

## Parent

.scratch/issues/0021-session-owned-usb-camera-source-feeds-prd.md

## What to build

Route selected USB camera processing, calibration, and background sampling through the shared USB Camera Source owner where touched. The Vision Pipeline remains single-selected-camera scoped, but it should not open a competing USB capture handle for the same physical Camera Source.

## Acceptance criteria

- [x] Selected USB processing acquires a shared owner lease instead of directly opening a duplicate USB capture handle.
- [x] USB calibration/background sampling consumes frames from the shared owner snapshot path.
- [x] Processing waits for fresh frame versions where possible and avoids repeatedly processing the same frame in a tight loop.
- [x] Frame snapshots are decoded for processing in the first iteration; raw mutable frame sharing is not introduced.
- [x] Hiding a tile does not stop a USB owner while selected-camera processing still holds a consumer lease.
- [x] This slice does not implement simultaneous Train State processing across multiple Camera Zones.
- [x] Tests cover no duplicate capture open during selected USB processing, calibration/shared-owner use, frame-version freshness, and hidden-but-included owner retention.

## Blocked by

- .scratch/issues/0022-session-owned-usb-feed-owner-baseline.md
- .scratch/issues/0025-usb-source-runtime-status-and-restart-action.md
