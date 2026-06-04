# Session-Owned USB Feed Owner Baseline

Type: AFK  
Labels: ready-for-agent

## Parent

.scratch/issues/0021-session-owned-usb-camera-source-feeds-prd.md

## What to build

Build the baseline session-owned USB Camera Source feed owner so visible USB camera tiles can consume frames from one owner per physical USB source instead of opening competing capture handles. The slice is complete when two visible USB Camera Sources can run through shared owners without duplicate opens, while Session cleanup stops and disposes owners.

## Acceptance criteria

- [x] A USB Camera Source owner manager enforces one owner per typed physical USB camera key and returns the existing owner/lease for repeated requests.
- [x] USB owners expose immutable latest-frame snapshots with source ID, timestamp, dimensions, encoded JPEG bytes, and a monotonically increasing frame version.
- [x] USB owner startup is serialized, each owner reads on its own task, and owners keep only the latest successful frame.
- [x] Visible USB camera tiles can render from the owner snapshot path without direct duplicate USB capture opens.
- [x] Owners stop and dispose when the Session ends or all cameras are cleared.
- [x] Tests use an injectable fake capture backend and cover duplicate owner prevention, startup/read behavior, snapshot versioning, and Session cleanup.

## Blocked by

None - can start immediately
