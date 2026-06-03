# Per-Source USB Capture Settings UI

Type: AFK  
Labels: ready-for-agent

## Parent

.scratch/issues/0021-session-owned-usb-camera-source-feeds-prd.md

## What to build

Add persisted per-USB-Camera-Source capture settings in the selected Camera Panel. Operators can choose preset resolution and target FPS values, apply or revert changes explicitly, and see requested settings separately from the actual runtime mode accepted by the camera.

## Acceptance criteria

- [ ] USB capture settings are stored separately from runtime processing settings and persisted per USB Camera Source.
- [ ] The selected Camera Panel shows USB capture settings only for USB Camera Sources.
- [ ] Resolution presets are 640x480, 1280x720, and 1920x1080, with target FPS choices 30 and 60.
- [ ] Capture settings edits are batched behind explicit Apply and Revert controls.
- [ ] Apply persists requested settings immediately; Revert restores the last persisted requested settings.
- [ ] Requested settings remain distinct from actual runtime mode, and fallback actual mode is shown when the camera runs differently than requested.
- [ ] Applying settings to a visible-only running USB source restarts only that source owner.
- [ ] Applying settings to a failed USB source does not auto-restart it; the operator must use Restart Camera Source.
- [ ] Tests cover persistence, USB-only UI projection, Apply/Revert behavior, requested-vs-actual mode, visible-only restart, and failed-source no-auto-restart.

## Blocked by

- .scratch/issues/0022-session-owned-usb-feed-owner-baseline.md
- .scratch/issues/0025-usb-source-runtime-status-and-restart-action.md
