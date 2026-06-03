# USB Discovery And Duplicate Protection

Type: AFK  
Labels: ready-for-agent

## Parent

.scratch/issues/0021-session-owned-usb-camera-source-feeds-prd.md

## What to build

Update USB Camera Source discovery and add-camera selection so already-added USB sources are visible as disabled entries and are not probed, while unknown candidate devices are still probed and can be added.

## Acceptance criteria

- [ ] USB discovery receives the set of already-added USB source identities and avoids probing those devices.
- [ ] The add USB camera dialog shows already-added USB Camera Sources as disabled or otherwise clearly unavailable with “already added” status.
- [ ] Duplicate USB Camera Sources are blocked by source identity.
- [ ] Unknown candidate USB indices are still probed and can be added when available.
- [ ] Tests cover already-added source display, no-probe behavior for added sources, duplicate blocking, and successful discovery of unknown candidates.

## Blocked by

- .scratch/issues/0022-session-owned-usb-feed-owner-baseline.md
