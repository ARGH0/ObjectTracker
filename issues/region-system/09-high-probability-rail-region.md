# Issue 9: HIGH_PROBABILITY_RAIL_REGION — complete vertical slice (grid → registry → evaluator → confidence boost)

**PRD Reference:** [Region System PRD](../../prd/region-system-prd.md)

## What to build

Complete end-to-end implementation of HIGH_PROBABILITY_RAIL_REGION behavior. Builds on the working EXCLUDE_REGION flow (#8).

Flow:
1. Operator creates HIGH_PROBABILITY_RAIL_REGION via Grid Editor Dialog, selects cells along known rail paths
2. Region saved and loaded into Registry
3. Vision Pipeline emits `TrainDetected` event
4. Region Evaluator maps train position to grid cell, checks if cell belongs to HIGH_PROBABILITY_RAIL_REGION
5. If train is in rail region → confidence score boosted by configurable factor (e.g., +0.2 or multiplier)

The confidence boost factor is configurable per-region or globally defined. The boosted confidence reflects the operator's knowledge that this area contains rails, making detections here more trustworthy.

## Acceptance criteria

- [ ] Operator can create HIGH_PROBABILITY_RAIL_REGION via Grid Editor Dialog
- [ ] Region persists and loads correctly in Registry
- [ ] Train detected within HIGH_PROBABILITY_RAIL_REGION cells receives confidence boost
- [ ] Confidence boost is configurable (per-region or global factor)
- [ ] Train detected outside rail region retains original confidence
- [ ] HIGH_PROBABILITY_RAIL_REGION has priority 10 — loses to EXCLUDE_REGION but overrides ENTER/EXIT_CROSSROAD and CAMERA_OVERLAP
- [ ] Boosted confidence applied before downstream logic (e.g., collision warnings, PLC mapping)
- [ ] Unit tests verify confidence boost calculation with mocked regions
- [ ] Integration test: full flow from dialog selection to boosted output

## Blocked by

- #8 (EXCLUDE_REGION)
