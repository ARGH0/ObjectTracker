# Camera Zone Identity And Source Rebind Foundation

Type: AFK  
Labels: ready-for-agent

## Parent

- .scratch/issues/0009-camera-zone-grid-layering-foundation-prd.md

## What to build

Add stable Camera Zone ownership so runtime camera sources bind to `CameraZoneId` instead of runtime source identifiers. When a new source is added, auto-create a Camera Zone and bind it. Provide explicit rebind to an existing Camera Zone so operators can swap sources without losing Camera Zone configuration.

## Acceptance criteria

- [ ] Adding a new source creates a new Camera Zone and binds the source to it.
- [ ] Operator can rebind a source to an existing Camera Zone explicitly.
- [ ] Camera Zone binding survives restart and remains stable even if runtime source details change.

## Blocked by

None - can start immediately.
