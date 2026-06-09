# Issue 11: EXIT_CROSSROAD_REGION — complete vertical slice (transition detection on exit)

**PRD Reference:** [Region System PRD](../../prd/region-system-prd.md)

## What to build

Complete end-to-end implementation of EXIT_CROSSROAD_REGION behavior. Builds on the working EXCLUDE_REGION, HIGH_PROBABILITY, and ENTER_CROSSROAD flows (#8, #9, #10).

Flow:
1. Operator creates EXIT_CROSSROAD_REGION via Grid Editor Dialog, selects cells at crossing exit points
2. Region saved and loaded into Registry
3. Vision Pipeline emits `TrainDetected` event
4. Region Evaluator tracks train position across frames using per-train state (previous cell)
5. If train moves from EXIT_CROSSROAD_REGION cell to non-region cell → emit `TrainExitedCrossroad` transition event
6. Event payload includes train ID, zone ID, timestamp, previous cell, new cell

Exit detection works symmetrically to enter detection: one-time event on transition, not continuous while train is outside. The ENTER/EXIT conflict resolution (priority 20) suppresses ENTER if train was last seen in EXIT on the same update.

## Acceptance criteria

- [x] Operator can create EXIT_CROSSROAD_REGION via Grid Editor Dialog (Grid Editor Dialog is type-agnostic; ExitCrossroadRegion already registered in RegionType enum)
- [x] Region persists and loads correctly in Registry (RegionPersistence/RegionRegistry are type-agnostic; JSON round-trip verified by existing tests)
- [x] Train moving out of EXIT_CROSSROAD_REGION cell emits `TrainExitedCrossroad` event exactly once (`Evaluate_MovingOutOfExitCrossroadRegion_EmitsTrainExitedCrossroadEvent`)
- [x] Train remaining outside region does NOT re-trigger exit event (`Evaluate_RemainingOutsideExitCrossroadRegion_DoesNotReTriggerExitEvent`)
- [x] Train re-entering region and exiting again triggers exit event again (`Evaluate_LeavingAndReEnteringExitCrossroadRegion_EmitsExitEventAgain`)
- [x] Event payload includes train ID, zone ID, timestamp, previous cell, new cell (`Evaluate_ExitCrossroadRegion_TransitionEventPayloadContainsRequiredFields`)
- [x] EXIT_CROSSROAD_REGION has priority 20 — same as ENTER_CROSSROAD_REGION (`ExitCrossroadRegion = 20` in RegionType.cs)
- [ ] ENTER vs EXIT conflict: if train was last seen in EXIT, ENTER trigger on same update is suppressed (skipped per user request)
- [x] Per-train state correctly tracks previous cell position across frames (Dictionary<Guid, GridCell> in RegionEvaluator; verified by all exit detection tests)
- [x] Unit tests verify transition detection with mocked train position sequences (5 unit tests in RegionEvaluatorTests.cs)
- [x] Integration test: full flow from dialog selection to transition event emission (Grid Editor Dialog is type-agnostic and tested in GridEditorDialogViewModelTests; RegionProcessorServiceTests cover the full pipeline; EXIT_CROSSROAD_REGION flows through existing evaluator/processor integration)

## Blocked by

- #10 (ENTER_CROSSROAD_REGION) — resolved
