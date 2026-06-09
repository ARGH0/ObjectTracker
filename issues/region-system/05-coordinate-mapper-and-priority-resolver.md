# Issue 5: Coordinate mapper and priority resolver — pixel-to-cell mapping + overlap resolution

**PRD Reference:** [Region System PRD](../../prd/region-system-prd.md)

## What to build

Two stateless singleton services:

**Coordinate Mapper (`ICoordinateMapper` / `CoordinateMapper`):**
- Pure function: converts process-frame pixel coordinates to grid cell indices
- Formula: `cellCol = floor(pixelX / processWidth * gridCols)`, `cellRow = floor(pixelY / processHeight * gridRows)`
- Parameters: `(pixelX, pixelY, processWidth, processHeight, gridCols, gridRows)` → `(cellCol, cellRow)`
- Handles edge cases: pixel at 0, pixel at width/height -1, exact cell boundaries

**Region Priority Resolver (`IRegionPriorityResolver` / `RegionPriorityResolver`):**
- Given a set of overlapping regions for a grid cell, returns the winning region type and ID
- Static priority table:
  1. EXCLUDE_REGION (priority 0) — blocks train output entirely
  2. HIGH_PROBABILITY_RAIL_REGION (priority 10) — confidence boost overrides normal classification
  3. ENTER_CROSSROAD_REGION / EXIT_CROSSROAD_REGION (priority 20) — conflict resolved by recency: if train was last seen in EXIT, ENTER trigger suppressed
  4. CAMERA_OVERLAP_REGION (priority 30) — informational, does not suppress or modify train state
- Operators who need finer control can delete conflicting Region or adjust cells manually

## Acceptance criteria

### Coordinate Mapper
- [ ] `Map(pixelX, pixelY, processWidth, processHeight, gridCols, gridRows)` returns correct cell indices
- [ ] Pixel at (0, 0) maps to cell (0, 0)
- [ ] Pixel at (width-1, height-1) maps to cell (gridCols-1, gridRows-1)
- [ ] Pixel at exact cell boundary: floor() produces correct cell on lower side
- [ ] Unit tests cover all boundary edge cases

### Priority Resolver
- [ ] `Resolve(cellRegions)` returns highest-priority region type
- [ ] EXCLUDE_REGION always wins over other types
- [ ] HIGH_PROBABILITY_RAIL_REGION wins over ENTER/EXIT_CROSSROAD_REGION and CAMERA_OVERLAP_REGION
- [ ] ENTER vs EXIT conflict: recency check suppresses ENTER if train was last in EXIT
- [ ] CAMERA_OVERLAP_REGION loses to all other types on same cell
- [ ] Table-driven tests for all 5 type combinations

## Blocked by

- #3 (Registry)
