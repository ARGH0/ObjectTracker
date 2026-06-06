# Unified Camera Source Feed Consumption

Type: AFK  
Labels: ready-for-agent

## Parent

.scratch/issues/0030-vision-pipeline-snapshot-frame-feed-prd.md

## What to build

Make raw feed display consume session-owned Camera Source feeds for both USB and file-based Camera Sources. Visible excluded Camera Sources and visible Camera Sources while the Vision Pipeline is stopped should use the shared Camera Source feed model.

## Acceptance criteria

- [ ] Visible excluded USB and file-based Camera Sources render from session-owned feeds.
- [ ] Visible included Camera Sources render raw feed while the Vision Pipeline is stopped.
- [ ] USB Camera Sources still use one owning feed and do not open duplicate physical captures.
- [ ] Tests cover USB and file-based raw feed consumers through the same feed-consumption behavior.

## Blocked by

- .scratch/issues/0032-session-owned-video-camera-source-feed.md
- .scratch/issues/0028-selected-usb-processing-uses-shared-feed.md
