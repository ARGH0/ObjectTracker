# Calibration Confidence To Monitor-Only Detection Path

Type: AFK  
Labels: ready-for-agent

## What to build

Connect calibration confidence and detection safety behavior so low-confidence calibration runs in monitor-only mode (no Automatic Action) while still providing detection visibility. The slice is complete when operators can continue monitoring while unsafe automatic behavior stays blocked.

## Acceptance criteria

- [ ] Low calibration confidence activates monitor-only behavior for detection-driven Automatic Action.
- [ ] Detection previews remain available while monitor-only is active.
- [ ] Status and audit outputs clearly communicate monitor-only state and reason.

## Blocked by

- .scratch/issues/0002-perspective-aware-safety-margin-in-rail-roi.md
- .scratch/issues/0006-motion-state-transition-policy-for-roi-evidence-loss.md
