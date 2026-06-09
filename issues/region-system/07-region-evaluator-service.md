# Issue 7: Region evaluator service — subscribes to pipeline events, delegates to mapper/resolver/registry

**PRD Reference:** [Region System PRD](../../prd/region-system-prd.md)

## What to build

Implement `IRegionEvaluator` / `RegionEvaluator` — the core logic that evaluates detected trains against active regions per zone. Injects `IRegionRegistry`, `ICoordinateMapper`, `IRegionPriorityResolver`.

The evaluator:
- Subscribes to `TrainDetected` events from the Vision Pipeline
- For each detected train, maps its position to grid cell indices using `ICoordinateMapper`
- Queries `IRegionRegistry.GetByZone(zoneId)` for all active regions in that zone
- Checks which regions contain the train's cell
- Uses `IRegionPriorityResolver` to resolve conflicts when multiple regions claim the same cell
- Applies region behavior based on type (filtering, confidence boosting, transition detection)
- Outputs enriched `TrainState` with filters, confidence adjustments, and transition events

The evaluator is stateless per-frame but maintains state for ENTER/EXIT_CROSSROAD_REGION transition tracking (previous cell position per train). This state lives in the evaluator and is updated each frame.

## Acceptance criteria

- [ ] Evaluates detected trains against all active regions in their Camera Zone
- [ ] Maps train positions to grid cells using `ICoordinateMapper`
- [ ] Resolves overlapping region conflicts using `IRegionPriorityResolver`
- [ ] Applies EXCLUDE filtering: trains in EXCLUDE_REGION cells are removed from output
- [ ] Applies confidence boosting: trains in HIGH_PROBABILITY_RAIL_REGION cells get boosted confidence
- [ ] Detects ENTER_CROSSROAD transition: train moves from non-region cell to region cell → emits transition event
- [ ] Detects EXIT_CROSSROAD transition: train moves from region cell to non-region cell → emits transition event
- [ ] Maintains per-train state for crossroad transition tracking (previous cell position)
- [ ] Outputs enriched `TrainState` with all applied region behaviors
- [ ] Unit tests verify each behavior type independently with mocked dependencies
- [ ] Integration test: full pipeline from event emission to enriched output

## Blocked by

- #3 (Registry), #5 (Mapper + Resolver), #6 (Pipeline events)
