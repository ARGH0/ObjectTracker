# Diffed USB Tile Consumers

Type: AFK  
Labels: ready-for-agent

## Parent

.scratch/issues/0021-session-owned-usb-camera-source-feeds-prd.md

## What to build

Replace all-or-nothing camera tile preview restarts with diffed USB tile consumers. Adding, hiding, showing, or removing one USB Camera Source should start or stop only the affected consumer while unchanged visible USB feeds continue running.

## Acceptance criteria

- [ ] Camera tile consumer orchestration diffs desired tile state and does not cancel/restart unchanged USB tile consumers on grid refresh.
- [ ] Adding a new visible USB Camera Source starts only the new source/consumer and does not interrupt existing visible USB feeds.
- [ ] Visible but excluded USB Camera Sources continue to render raw live feed without Train State annotations.
- [ ] Hidden and excluded USB Camera Sources release their owner after a two-second grace period, canceling the stop if a consumer returns.
- [ ] Video file camera playback remains unchanged by the USB tile consumer changes.
- [ ] Tests cover adding one USB source without stopping existing consumers, visibility toggles, grace-period release, and video-file non-regression.

## Blocked by

- .scratch/issues/0022-session-owned-usb-feed-owner-baseline.md
