# USB Source Runtime Status And Restart Action

Type: AFK  
Labels: ready-for-agent

## Parent

.scratch/issues/0021-session-owned-usb-camera-source-feeds-prd.md

## What to build

Add per-camera USB Camera Source runtime status and a targeted Restart Camera Source action. Operators should see starting, running, stale, and failed feed status, get clear placeholders for non-live states, and be able to restart one failed USB source without restarting all feeds.

## Acceptance criteria

- [ ] USB owner runtime status is projected per Camera Source as stopped, starting, running, failed, and derived stale status after about one second without a successful frame.
- [ ] Starting and restarting USB tiles show a placeholder rather than stale live video.
- [ ] Failed USB tiles show a prominent failed state while retaining the Camera Source in the Session list.
- [ ] Frame age is shown only for stale or failed USB sources, not normal running feeds.
- [ ] USB source failures are per-camera status only and do not raise an Ambiguity Alert.
- [ ] Restart Camera Source exists in the selected Camera Panel for USB sources and is disabled while that source is actively processed by the Vision Pipeline.
- [ ] Restart preserves previous failure details while starting and updates status on success or new failure.
- [ ] Tests cover status projection, stale derivation, failed placeholders, Ambiguity Alert non-interaction, and restart enablement/behavior.

## Blocked by

- .scratch/issues/0022-session-owned-usb-feed-owner-baseline.md
- .scratch/issues/0023-diffed-usb-tile-consumers.md
