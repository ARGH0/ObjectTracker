# App-Wide Grid Settings With Default 32x18

Type: AFK  
Labels: ready-for-agent

## Parent

- .scratch/issues/0009-camera-zone-grid-layering-foundation-prd.md

## What to build

Add dedicated application settings for shared grid dimensions used by all Camera Zones. Default to `32x18`, allow operator configuration, and persist to dedicated app settings storage.

## Acceptance criteria

- [ ] Grid settings are stored in dedicated app settings, separate from Camera Zone settings.
- [ ] Default grid is `32x18` when no app settings exist.
- [ ] Updated grid values persist and reload across Sessions.

## Blocked by

None - can start immediately.
