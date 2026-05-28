# Detection Regression Fixtures And End-To-End Checks

Type: AFK  
Labels: ready-for-agent

## What to build

Create repeatable detection regression checks using representative fixtures and end-to-end verification for known failure modes (Train visible but no moving objects, and fragmented thin-strip detection). The slice is complete when detection behavior can be validated consistently after future tuning changes.

## Acceptance criteria

- [ ] Regression fixtures cover key failure modes observed in Rail ROI detection.
- [ ] End-to-end checks validate connected Train motion behavior and state-transition expectations.
- [ ] Results are documented in a form that allows repeatable verification during future changes.

## Blocked by

- .scratch/issues/0002-perspective-aware-safety-margin-in-rail-roi.md
- .scratch/issues/0004-bounded-hole-fill-and-component-merge-policy.md
- .scratch/issues/0006-motion-state-transition-policy-for-roi-evidence-loss.md
- .scratch/issues/0007-calibration-confidence-to-monitor-only-detection-path.md
