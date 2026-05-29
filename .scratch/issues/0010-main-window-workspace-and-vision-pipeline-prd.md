# Main Window Workspace And Vision Pipeline PRD

Type: AFK  
Labels: ready-for-agent

## What to build

Adopt and publish the main window redesign PRD covering workspace behavior, Vision Pipeline controls, camera visibility/inclusion semantics, and safety signaling.

PRD: `docs/prd/main-window-workspace-and-vision-pipeline-prd.md`

The slice is complete when implementation planning and execution track against the PRD decisions for:

- persistent top menu + bottom status bar shell behavior
- center workspace switching between Camera, Layers, and Settings
- Vision Pipeline dropdown control behavior and pending restart semantics
- Camera Panel ownership of camera-level visibility/debug/inclusion controls
- global Layer Type ownership with read-only camera usage visibility and in-use delete blocking
- explicit settings save/guard flow and per-setting apply policy tags

## Acceptance criteria

- [ ] PRD is complete and reflects resolved design decisions from the grilling session.
- [ ] PRD is published and discoverable from the issue tracker.
- [ ] Issue is labeled `ready-for-agent`.

## Notes

- This issue tracks PRD publication/readiness only, not implementation.
