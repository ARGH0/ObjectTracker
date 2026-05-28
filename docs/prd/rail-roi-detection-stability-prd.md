# PRD: Rail ROI Detection Stability Improvements

## Problem Statement

Operators need stable Train motion detection in each Camera Zone, but the current motion pipeline often produces fragmented or missing Train blobs. In real scenes, rail texture and low grayscale contrast between Train and track cause weak foreground masks, so moving Trains are split into thin components or dropped below area thresholds. This reduces Confidence and increases Ambiguity Alert risk for downstream Automatic Action.

## Solution

Improve motion detection by introducing Rail ROI-gated foreground extraction and stronger Train connectivity logic, while preserving safety semantics. The updated detection flow should prioritize recovering full Train envelopes (with Safety Margin) and produce stable connected motion components suitable for Train State updates. The solution stays focused on detection behavior, not Train identity mapping.

## User Stories

1. As an operator, I want Rail ROI to bound motion detection, so that off-track scene changes do not compete with Train motion.
2. As an operator, I want Rail ROI to cover full Train envelope plus Safety Margin, so that Train overhang and sway are still detected.
3. As an operator, I want Safety Margin to be perspective-aware, so that near and far rail segments are both reliable.
4. As an operator, I want to tune Safety Margin by rail segment, so that difficult curves and crossings can be corrected without overexpanding all zones.
5. As an operator, I want motion connectivity to join rail-caused gaps, so that one moving Train appears as one connected object.
6. As an operator, I want bounded merge rules, so that two nearby Trains are not incorrectly merged into one.
7. As an operator, I want bounded hole-fill in Train blobs, so that small interior rail holes do not fragment motion.
8. As an operator, I want morphology order optimized for weak Train contrast, so that Train pixels are connected before cleanup removes them.
9. As an operator, I want practical default thresholds for low-contrast scenes, so that detection works before heavy manual tuning.
10. As an operator, I want per-camera runtime tuning, so that each Camera Zone can adapt to its own lighting and perspective.
11. As an operator, I want detection previews that show Rail ROI effect clearly, so that calibration acceptance is faster and less ambiguous.
12. As an operator, I want weak or lost Train evidence near ROI edges to become `uncertain` first, so that temporary drops do not instantly become `gone-from-track`.
13. As an operator, I want `gone-from-track` only after an uncertainty window expires, so that safety fallbacks are deliberate and predictable.
14. As an operator, I want monitor-only fallback when calibration confidence is low, so that I retain visibility without unsafe Automatic Action.
15. As an operator, I want per-session calibration behavior to remain explicit and auditable, so that detection quality changes are explainable.
16. As an operator, I want detection improvements to work without requiring train reference photos, so that motion reliability is independent of identity aids.
17. As an integrator, I want motion output to be stable enough for downstream Train State logic, so that Confidence and Motion State transitions are trustworthy.
18. As a maintainer, I want detection logic split into deep modules, so that algorithm behavior is testable without UI dependencies.

## Implementation Decisions

- Adopt Rail ROI as a first-class detection gate before contour extraction, scoped per Camera Zone.
- Define Rail ROI semantics as full Train envelope plus Safety Margin (not visible-rails-only coverage).
- Use perspective-aware Safety Margin with segment-level adjustability and conservative defaults.
- Keep ROI creation hybrid: auto-suggest from calibration or baked background, then operator adjustment and acceptance.
- Reorder morphology for fragmented masks: threshold, close-first connectivity, optional light open cleanup, contour extraction.
- Add bounded connectivity policy:
  - bounded hole fill for small internal gaps
  - bounded component merge by distance and corridor or segment constraints
  - reject merges that violate expected Train envelope continuity
- Keep detection pipeline grayscale-diff based for now, but retune defaults toward weak-contrast scenes (lower threshold and smaller minimum motion area than current strict settings).
- Preserve per-camera tuning controls and persistence model so each Camera Zone can maintain independent operating values.
- Align motion loss behavior with domain semantics:
  - ROI-edge or weak-evidence loss transitions to `uncertain` first
  - transition to `gone-from-track` only after timeout and no recovery or Operator Intervention
- Keep identity and photo-reference work out of this implementation; optional Train reference images are a later Confidence enhancement, not a detection prerequisite.

### Deep Modules

- RailRoiMaskService: owns ROI rasterization, Safety Margin expansion, and segment-aware ROI mask output.
- MotionMaskRefiner: owns close-first morphology, hole-fill, and bounded merge logic.
- MotionStateTransitionPolicy: owns `moving`, `stationary`, `uncertain`, and `gone-from-track` transition timing rules driven by detection evidence.
- DetectionTuningProfile: owns per-camera parameter set, defaults, and validation boundaries.

## Testing Decisions

- Good tests should assert externally visible behavior (mask connectivity, component counts, state transitions), not internal OpenCV call order.
- Prioritize tests for deep modules:
  - RailRoiMaskService: ROI includes full Train envelope and Safety Margin per segment.
  - MotionMaskRefiner: close-first plus bounded fill and merge turns fragmented Train pixels into stable single components without cross-Train merges.
  - MotionStateTransitionPolicy: evidence-loss transitions to `uncertain`, then `gone-from-track` only after configured timeout.
  - DetectionTuningProfile: parameter validation and camera-specific fallback defaults.
- Use fixture-driven tests with representative grayscale masks or frames that mimic rail texture fragmentation and weak contrast.
- Add regression scenarios from observed failures:
  - Train visible but no moving objects
  - Train detected as multiple thin strips
- Favor deterministic synthetic masks for unit tests and a small number of recorded-frame integration checks for end-to-end confidence.
- There is little existing automated test prior art in this repo for this pipeline, so introduce focused module-level tests first, then add lightweight integration coverage.

## Out of Scope

- Train identity mapping improvements (Local Train ID to PLC ID workflows).
- Train reference photo ingestion or classification changes.
- Multi-camera handoff identity continuity logic.
- Major UI redesign beyond what is required to expose ROI and tuning behavior.
- Non-detection architectural changes unrelated to Rail ROI motion extraction.
- Replacing the core background-diff approach with a fundamentally new detector model.

## Further Notes

- This PRD intentionally uses domain terms from `CONTEXT.md` (Rail ROI, Safety Margin, Train State, Motion State, Confidence, Automatic Action, Operator Intervention, Ambiguity Alert).
- ADR context is respected: session calibration with monitor-only fallback remains the safety baseline.
- Session decisions already locked in this planning thread are reflected here: full Train envelope Rail ROI, perspective-aware Safety Margin, close-first connectivity, and `uncertain` before `gone-from-track`.
