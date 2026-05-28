# Motion State Transition Policy For ROI Evidence Loss

Type: AFK  
Labels: ready-for-agent

## What to build

Implement detection-driven Motion State transition behavior so ROI-edge or weak-evidence loss moves a Train to `uncertain` first, then to `gone-from-track` only after the configured uncertainty window expires without recovery or Operator Intervention. The slice is complete when this transition path is visible and auditable.

## Acceptance criteria

- [ ] Detection evidence loss near Rail ROI transitions Motion State to `uncertain` before any terminal state.
- [ ] `gone-from-track` is emitted only after uncertainty timeout expires unresolved.
- [ ] Transition events are observable in runtime outputs and session audit records.

## Blocked by

- .scratch/issues/0004-bounded-hole-fill-and-component-merge-policy.md
