# Camera Zone Grid Layering Foundation PRD

Type: AFK  
Labels: ready-for-agent

## What to build

Adopt the PRD for Camera Zone grid layering foundation and use it as the implementation contract for the next delivery phase.

PRD: `docs/prd/camera-zone-grid-layering-prd.md`

The slice is complete when implementation planning and execution track against the PRD decisions for:

- app-wide grid configuration (`32x18` default)
- app-wide layer type policy (precedence + merge behavior)
- stable `CameraZoneId` ownership
- per-Camera Zone multi-layer and region authoring with persistence
- UI-first, persistence-first scope without live tracking integration

## Acceptance criteria

- [ ] PRD is complete and reflects resolved design decisions from the grilling session.
- [ ] PRD is published and discoverable from the issue tracker.
- [ ] Issue is labeled `ready-for-agent`.

## Notes

- This issue intentionally tracks PRD publication and adoption readiness, not implementation itself.
