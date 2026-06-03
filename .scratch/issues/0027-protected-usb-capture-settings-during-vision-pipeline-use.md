# Protected USB Capture Settings During Vision Pipeline Use

Type: AFK  
Labels: ready-for-agent

## Parent

.scratch/issues/0021-session-owned-usb-camera-source-feeds-prd.md

## What to build

Protect USB capture setting changes while the Vision Pipeline is actively processing the selected USB Camera Source. Applied settings should persist as requested intent, mark pending Vision Pipeline restart, and apply safely after processing stops when the camera still needs a live owner.

## Acceptance criteria

- [ ] Applying USB capture settings while that Camera Source is actively processed by the Vision Pipeline does not restart the physical source immediately.
- [ ] The requested settings are persisted and shown in controls while active runtime mode remains visible as status.
- [ ] Pending USB capture settings mark pending Vision Pipeline restart in the existing pending-restart signaling.
- [ ] When the Vision Pipeline stops and the camera is still visible, pending settings apply by restarting only that USB owner.
- [ ] If the camera is hidden and excluded when processing stops, no owner is started until the next visibility or processing need.
- [ ] Tests cover protected apply, requested-vs-active display, pending restart signaling, safe apply after stop, and hidden/excluded deferred start.

## Blocked by

- .scratch/issues/0026-per-source-usb-capture-settings-ui.md
