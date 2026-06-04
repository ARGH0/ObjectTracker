# End-To-End USB Source Regression Coverage

Type: AFK  
Labels: completed

## Parent

.scratch/issues/0021-session-owned-usb-camera-source-feeds-prd.md

## What to build

Add end-to-end-style regression coverage for the full session-owned USB Camera Source feed behavior. The suite should prove the main operator flows across adding multiple USB sources, raw tile continuity, duplicate/discovery behavior, runtime status, settings, selected processing, and file playback non-regression.

## Acceptance criteria

- [x] Regression coverage proves adding multiple USB Camera Sources does not halt existing visible USB feeds.
- [x] Regression coverage proves duplicate USB source handling and already-added discovery behavior.
- [x] Regression coverage proves starting, stale, failed, and restart status behavior in operator-facing projections.
- [x] Regression coverage proves USB capture settings Apply/Revert, requested-vs-actual display, pending Vision Pipeline restart behavior, and failed-source no-auto-restart behavior.
- [x] Regression coverage proves selected USB processing/calibration uses the shared owner lease rather than opening a duplicate capture.
- [x] Regression coverage proves video file camera playback behavior remains unchanged.
- [x] Tests use fake capture backends and service-level projections where possible rather than relying on physical USB cameras or private UI event wiring.

## Blocked by

- .scratch/issues/0023-diffed-usb-tile-consumers.md
- .scratch/issues/0024-usb-discovery-and-duplicate-protection.md
- .scratch/issues/0025-usb-source-runtime-status-and-restart-action.md
- .scratch/issues/0026-per-source-usb-capture-settings-ui.md
- .scratch/issues/0027-protected-usb-capture-settings-during-vision-pipeline-use.md
- .scratch/issues/0028-selected-usb-processing-uses-shared-feed.md
