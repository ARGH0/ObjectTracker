# Issue 10: ENTER_CROSSROAD_REGION — complete vertical slice (transition detection on entry)

**PRD Reference:** [Region System PRD](../../prd/region-system-prd.md)

## What to build

Complete end-to-end implementation of ENTER_CROSSROAD_REGION behavior. Builds on the working EXCLUDE_REGION and HIGH_PROBABILITY flows (#8, #9).

Flow:
1. Operator creates ENTER_CROSSROAD_REGION via Grid Editor Dialog, selects cells at crossing entry points
2. Region saved and loaded into Registry
3. Vision Pipeline emits `TrainDetected` event
4. Region Evaluator tracks train position across frames using per-train state (previous cell)
5. If train moves from non-region cell to ENTER_CROSSROAD_REGION cell → emit `TrainEnteredCrossroad` transition event
6. Event payload includes train ID, zone ID, timestamp, previous cell, new cell

The transition event is emitted only once per entry (not continuously while train remains in region). Exit from the region resets the state so re-entry triggers another transition.

## Acceptance criteria

- [x] Operator can create ENTER_CROSSROAD_REGION via Grid Editor Dialog (existing from #4)
- [x] Region persists and loads correctly in Registry (existing from #2, #3)
- [x] Train moving into ENTER_CROSSROAD_REGION cell emits `TrainEnteredCrossroad` event exactly once
- [x] Train remaining in region does NOT re-trigger entry event
- [x] Train leaving region and re-entering triggers entry event again
- [x] Event payload includes train ID, zone ID, timestamp, previous cell, new cell
- [x] ENTER_CROSSROAD_REGION has priority 20 — loses to EXCLUDE_REGION and HIGH_PROBABILITY_RAIL_REGION
- [x] Per-train state correctly tracks previous cell position across frames
- [x] Unit tests verify transition detection with mocked train position sequences
- [ ] Integration test: full flow from dialog selection to transition event emission

## Blocked by

- #8 (EXCLUDE_REGION), #9 (HIGH_PROBABILITY_RAIL_REGION)
