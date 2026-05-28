# Per-Camera Detection Tuning Defaults And Persistence

Type: AFK  
Labels: ready-for-agent

## What to build

Deliver per-camera detection tuning defaults optimized for weak-contrast Train motion and ensure the values persist cleanly across sessions. The slice is complete when each Camera Zone can keep its own stable detection profile without affecting other zones.

## Acceptance criteria

- [ ] Detection defaults are adjusted for weak-contrast Train scenes (threshold, motion area, and compatible morphology settings).
- [ ] Per-camera tuning values can be edited and persisted across runs.
- [ ] Detection preview confirms settings are applied independently per Camera Zone.

## Blocked by

- .scratch/issues/0003-close-first-motion-mask-refinement.md
