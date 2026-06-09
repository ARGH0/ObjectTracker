# Issue 8: EXCLUDE_REGION — complete vertical slice (grid → registry → evaluator → filtered output)

**PRD Reference:** [Region System PRD](../../prd/region-system-prd.md)

## What to build

Complete end-to-end implementation of EXCLUDE_REGION behavior. This is the first full vertical slice that demonstrates the entire Region system pipeline working together.

Flow:
1. Operator opens Grid Editor Dialog (#4), selects cells on camera feed, creates Region with type EXCLUDE_REGION
2. Region saved to `regions.json` via Persistence (#2)
3. Region loaded into Registry (#3) at startup
4. Vision Pipeline emits `TrainDetected` event (#6)
5. Region Evaluator (#7) maps train position to grid cell, checks if cell belongs to EXCLUDE_REGION
6. If train is in excluded cell → train is filtered from output (not shown to operator, not passed downstream)

This slice validates the entire system works: UI → persistence → registry → pipeline events → evaluator → filtered output.

## Acceptance criteria

- [x] Operator can create EXCLUDE_REGION via Grid Editor Dialog with selected cells
- [x] Region persists to `regions.json` and loads correctly on restart
- [x] Region appears in Registry with correct type and zone association
- [x] Train detected within EXCLUDE_REGION cells is filtered from Vision Pipeline output
- [x] Train detected outside EXCLUDE_REGION cells passes through normally
- [x] EXCLUDE_REGION has priority 0 (highest) — blocks all other region behaviors on same cell
- [x] Multiple EXCLUDE_REGIONs on same zone: union of all excluded cells filters trains
- [x] Unit tests verify filtering logic with mocked regions and train positions
- [ ] Integration test: full flow from dialog selection to filtered output
- [ ] Session audit log records EXCLUDE_REGION create/edit/delete actions

## Blocked by

- #3 (Registry), #4 (Grid editor), #5 (Mapper + Resolver), #7 (Evaluator)
