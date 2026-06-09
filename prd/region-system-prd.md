# Region System for Object Tracker

## Problem Statement

The Operator Tracker currently detects and tracks LEGO trains through a Vision Pipeline, but it has no way for the operator to define semantic regions on the camera feed that influence tracking behavior. The existing layer system (Layer Types, Camera Zone Layers, EffectiveZoneCompositionService) was designed for mask composition but is not actively used by the engine. There is no mechanism to:

- Exclude trains from detection in specific areas
- Boost confidence when trains appear in known rail zones
- Detect entry and exit transitions at crossroads
- Perform cross-camera object handoff in overlapping views

Without a Region system, the Vision Pipeline produces raw Train State with no spatial semantics beyond the basic Rail ROI mask. The operator cannot tell the system "this area is a crossing entry" or "ignore trains in this corner."

## Solution

Build a new Region system that overlays a fixed rectangular cell grid on each Camera Zone and lets the operator define regions by clicking individual cells. Each region has a type that determines its behavioral effect on Train State during the Vision Pipeline:

- **EXCLUDE_REGION**: Masks out all detected trains within this area from the output pipeline
- **HIGH_PROBABILITY_RAIL_REGION**: Marks areas where rails are likely located; motion here boosts confidence scores or triggers specialized classification logic
- **ENTER_CROSSROAD_REGION**: Defines entry points into a Conflict Zone; detects when trains transition from outside to inside
- **EXIT_CROSSROAD_REGION**: Defines exit points from a Conflict Zone; detects when trains transition from inside to outside
- **CAMERA_OVERLAP_REGION**: Marks physical areas visible across multiple Camera Zones; used for cross-camera object handoff and unified tracking state synchronization

Regions are stored, edited, and persisted independently of the Vision Pipeline. The system applies region behavior through a post-engine event callback that subscribes to detected train events, evaluates them against active regions, and emits enriched Train State.

## User Stories

1. As an operator, I want to click individual grid cells on a camera feed to toggle their selection, so that I can visually define a Region without drawing tools.
2. As an operator, I want to group selected cells into a named Region with a chosen type, so that the system knows how to treat trains in that area.
3. As an operator, I want to edit a Region's cells after creation by reopening the grid editor, so that I can refine the boundary without deleting and recreating the Region.
4. As an operator, I want to delete a Region entirely, so that it stops influencing the Vision Pipeline and frees up its grid cells for other Regions.
5. As an operator, I want to see which Region type each grid cell belongs to in the editor, so that I can verify my layout before saving.
6. As an operator, I want to save a Region definition to disk, so that it persists across Sessions and is restored automatically on next startup.
7. As an operator, I want all Region definitions loaded from disk at Session start, so that I do not need to redefine them after each restart.
8. As an operator, I want Regions scoped to a specific Camera Zone, so that I do not accidentally define regions for the wrong camera view.
9. As an operator, I want to create a Region that spans multiple Camera Zones (CAMERA_OVERLAP_REGION), so that the system can perform cross-camera object handoff.
10. As the Vision Pipeline, I want each detected train's position evaluated against all active Regions in its Camera Zone, so that region behavior can be applied per train update.
11. As the Vision Pipeline, I want trains detected within an EXCLUDE_REGION to be removed from the output, so that the operator does not see detections in masked-off areas.
12. As the Vision Pipeline, I want trains detected within a HIGH_PROBABILITY_RAIL_REGION to receive a confidence boost, so that the system's trust level reflects known rail geometry.
13. As the Vision Pipeline, I want ENTER_CROSSROAD_REGION to emit a transition event when a train moves from outside to inside the region, so that downstream logic can trigger crossing-aware behavior.
14. As the Vision Pipeline, I want EXIT_CROSSROAD_REGION to emit a transition event when a train moves from inside to outside the region, so that downstream logic can trigger crossing-aware behavior.
15. As the Vision Pipeline, I want CAMERA_OVERLAP_REGION to detect when the same physical train appears in overlapping regions on two different Camera Zones, so that tracks can be merged and deduplicated.
16. As the operator, I want a clear priority rule when a single grid cell belongs to multiple Regions of different types, so that conflicting behaviors are resolved deterministically.
17. As the operator, I want the system to handle window resizing and video resolution changes without breaking region-to-train coordinate mapping, so that Regions remain accurate under all display conditions.
18. As the system, I want Region evaluation to scale efficiently with many Regions and many tracked trains across multiple Camera Zones, so that performance remains acceptable during active Sessions.
19. As an operator, I want a standalone dialog window for grid editing that is decoupled from the main application window, so that I can focus on Region layout without navigating the full UI.
20. As a developer, I want Region data structures and logic to live in testable modules with clean interfaces, so that I can write unit tests for region behavior without running the full Vision Pipeline.
21. As the operator, I want the Region system to coexist with the existing Rail ROI mask (generated automatically from background estimation), so that both spatial filters operate together.
22. As an operator, I want to see a count of selected cells and the effective Region type in the grid editor, so that I can verify my selection before saving.
23. As the system, I want Region state changes (create, edit, delete) to be logged in the Session audit log, so that there is an immutable record of operator Region actions.

## Implementation Decisions

### Decision 1: Separate Region System (Not Layer Extension)

A new Region system will be built from scratch. The existing layer system (LayerType, CameraZoneLayer, EffectiveZoneCompositionService, LayerTypeCatalogService, etc.) will be deleted. Layers served a different purpose (mask composition) and are not actively used by the engine. The Region system is a fresh implementation with its own data structures, UI, persistence, and processing hooks.

### Decision 2: Regions Replace Layers Entirely

The old layer code is deleted. No backward compatibility or migration path is needed. The Region system fully supersedes what layers attempted to do, with richer semantics (5 behavioral types vs. generic precedence categories).

### Decision 3: Rebuild Grid From Scratch

The existing `GridDrawerDialog` and its tightly coupled code-behind implementation (~3400 line MainWindow.axaml.cs) will be replaced. The new grid overlay will be a clean standalone component with proper MVVM separation. No reuse of the old grid infrastructure.

### Decision 4: Per-Camera-Zone Grouping with Global Registry

Regions are stored in a single global registry (one flat data structure), but logically grouped by CameraZoneId. Each zone has a collection of associated regions. CAMERA_OVERLAP_REGION entries reference multiple zone IDs to express cross-zone relationships. This provides simple iteration over all regions while maintaining clear zone scoping.

### Decision 5: UUID-Based Region Identity

Each Region receives a GUID on creation. The operator-visible name is a separate field. This allows renaming, exporting, cross-referencing from other systems, and handling CAMERA_OVERLAP_REGION which spans zones. Deterministic natural keys (type + zone + cells) are rejected because they break when regions are renamed or merged.

### Decision 6: Arbitrary Cell Sets

A Region is any subset of grid cells — connected or disconnected. No connectivity enforcement. This maximizes flexibility for the operator and avoids implementing graph traversal or union-find during cell selection. If disconnected cells become problematic later, validation can be added.

### Decision 7: Standalone Dialog Component for Grid UI

The grid editor is a separate Avalonia dialog/window that opens on demand. It takes a camera frame image and grid settings as input, renders the overlay with clickable cells, and returns the selected cell set. Minimal coupling to MainWindow. The operator opens it, edits cells, saves or cancels, and closes it.

### Decision 8: Scale Factor Coordinate Mapping

Grid-to-processing coordinate mapping uses simple scale factors per zone. Given a train's bounding box center at `(x, y)` in process-frame coordinates, the cell indices are computed as:

```
cellCol = floor(x / processWidth * gridCols)
cellRow = floor(y / processHeight * gridRows)
```

The system stores `processWidth`, `processHeight`, `gridCols`, and `gridRows` per zone. No precomputed lookup tables or pixel-to-cell arrays are needed. This works for any resolution as long as the grid fills the frame proportionally.

### Decision 9: Post-Engine Event Callback for Region Behavior

The Vision Pipeline (BackgroundEstimationEngine) emits detected trains as events. A separate `RegionProcessor` service subscribes to these events, evaluates each train against active Regions in its Camera Zone, and emits enriched Train State. This keeps the engine at 890 lines manageable and gives the Region logic a pure-function interface: input = detected train + region definitions; output = processed Train State with filters, confidence adjustments, and transition events applied.

### Decision 10: Static Priority by Region Type for Overlap Resolution

When a grid cell belongs to multiple Regions of different types, the highest-priority type wins based on a static priority table:

1. **EXCLUDE_REGION** (priority 0 — highest) — blocks train output entirely
2. **HIGH_PROBABILITY_RAIL_REGION** (priority 10) — confidence boost overrides normal classification
3. **ENTER_CROSSROAD_REGION / EXIT_CROSSROAD_REGION** (priority 20) — conflict between them resolved by recency: if a train was last seen in EXIT, an ENTER trigger on the same update is suppressed
4. **CAMERA_OVERLAP_REGION** (priority 30 — lowest) — informational; does not suppress or modify train state

Operators who need finer control can delete the conflicting Region or adjust cells manually.

### Decision 11: Single `regions.json` Persistence File

All Regions are persisted in a single JSON file at `%APPDATA%\ObjectTracker\regions.json`. Structure:

```json
{
  "regions": [
    {
      "id": "guid-string",
      "name": "North Crossing Entry",
      "type": "ENTER_CROSSROAD_REGION",
      "cameraZoneId": "zone-abc-123",
      "cells": [{ "column": 12, "row": 5 }, { "column": 13, "row": 5 }],
      "createdAt": "2026-06-09T10:00:00Z",
      "updatedAt": "2026-06-09T14:30:00Z"
    }
  ]
}
```

CAMERA_OVERLAP_REGION entries include an additional `overlappingZoneIds` array. A single file is simpler to backup, migrate, and reason about than per-zone files.

### Decision 12: Explicit Handoff Event for CAMERA_OVERLAP_REGION

When a train is present in a CAMERA_OVERLAP_REGION on Camera Zone A and simultaneously appears in the same physical region on Camera Zone B within a configurable time window (default 500ms), a `TrackHandoff` event is emitted with:

- Source zone ID
- Target zone ID
- Object signature (Train Color, position within overlap region)
- Timestamp of first detection (source) and second detection (target)
- Confidence values from both zones

A `HandoffResolver` matches the source and target, merges the Local Train IDs into a unified track, and suppresses the duplicate on the target zone. The merged state includes confidence averaging and motion state reconciliation.

### Decision 13: Dependency Injection for All Services

All new services use constructor injection via a DI container (Microsoft.Extensions.DependencyInjection). No service creates its dependencies with `new`. Every interface is registered once at startup; every consumer only knows interfaces, never concrete types. This enables testability (swap implementations in tests), loose coupling, and clear dependency boundaries.

**Interface contract rules:**

- Every public service exposes an interface (`IRegionRegistry`, `IRegionPersistence`, `IRegionPriorityResolver`, `ICoordinateMapper`, `IRegionEvaluator`, `IHandoffResolver`, `IRegionProcessorService`).
- Interfaces are in the `Region.Model` or `Region.Contracts` namespace (no implementation details).
- Interfaces are immutable: no setters on domain types, methods return new values rather than mutating state.
- The concrete implementations live in `Region.Implementation` and are registered at application startup.

**DI registration order (startup):**

1. **Model types** — registered as singletons (pure data, no dependencies)
2. **Coordinate Mapper** — singleton (stateless pure function)
3. **Region Priority Resolver** — singleton (stateless priority table lookup)
4. **Region Persistence** — singleton (file I/O, owns file handle/lifecycle)
5. **Region Registry** — singleton (in-memory store, initialized by loading from Persistence)
6. **Handoff Resolver** — singleton (stateful across frames for time-window matching)
7. **Region Evaluator** — singleton (reads from Registry, uses Mapper + Priority Resolver)
8. **Region Processor Service** — singleton (orchestrates Evaluator + Handoff Resolver, subscribes to Vision Pipeline events)
9. **Region Manager UI Service** — singleton (binds Registry to UI, opens Grid Editor Dialog)
10. **Grid Editor Dialog** — transient (new instance per open; receives camera frame + grid settings via constructor or dialog parameters)

**Testability implications:**

- Unit tests create a `ServiceCollection`, register only the module under test with mock/fake dependencies, and resolve from an `IServiceProvider`.
- Integration tests register the full pipeline and exercise end-to-end flows (load regions → evaluate trains → verify output).
- No test creates real file I/O for persistence tests; a `FakeRegionPersistence` in-memory implementation is used instead.

### Modules to Build / Modify

**New modules (interfaces + implementations):**

1. **Region Model** (`Region.Model`) — Pure data types: `Region`, `RegionType`, `GridCell`, `CameraZoneId`. No logic, no dependencies. Immutable records/structs.
2. **Region Registry** (`IRegionRegistry` / `RegionRegistry`) — In-memory store for all regions with CRUD operations (create, update, delete, query by zone). Thread-safe for concurrent reads during Vision Pipeline execution via immutable snapshots.
3. **Region Persistence** (`IRegionPersistence` / `RegionPersistence`) — JSON serialization/deserialization for `regions.json`. Load at startup, save on mutation. File I/O encapsulated behind interface.
4. **Region Priority Resolver** (`IRegionPriorityResolver` / `RegionPriorityResolver`) — Given a set of overlapping regions for a grid cell, returns the winning region type and ID based on the static priority table. Stateless singleton.
5. **Coordinate Mapper** (`ICoordinateMapper` / `CoordinateMapper`) — Converts process-frame pixel coordinates to grid cell indices using scale factors. Pure function: `(pixelX, pixelY, processWidth, processHeight, gridCols, gridRows) -> (cellCol, cellRow)`. Stateless singleton.
6. **Region Evaluator** (`IRegionEvaluator` / `RegionEvaluator`) — Core logic that evaluates detected trains against active regions per zone. Injects `IRegionRegistry`, `ICoordinateMapper`, `IRegionPriorityResolver`. Applies EXCLUDE filtering, confidence boosting, enter/exit transition detection, and CAMERA_OVERLAP overlap detection. Outputs enriched Train State.
7. **Handoff Resolver** (`IHandoffResolver` / `HandoffResolver`) — Processes TrackHandoff events from CAMERA_OVERLAP_REGION evaluation. Merges tracks across zones, deduplicates, and emits unified Train State. Stateful (tracks pending handoffs within time window). Singleton.
8. **Region Processor Service** (`IRegionProcessorService` / `RegionProcessorService`) — Orchestrator that subscribes to Vision Pipeline detected train events, delegates to `IRegionEvaluator` and `IHandoffResolver`, and emits final processed results. The single subscription point for the Vision Pipeline. Singleton.
9. **Grid Editor Dialog** — Standalone Avalonia dialog with clean MVVM architecture. Renders camera frame, overlays grid, handles click-to-toggle cell selection, displays region color coding, and returns selected cells. Transient per open; receives camera frame + grid settings via constructor or dialog parameters.
10. **Region Manager UI Service** (`IRegionManagerService` / `RegionManagerService`) — Binds the `IRegionRegistry` to the UI workspace: list of regions per zone, create/edit/delete actions, open Grid Editor Dialog integration. Singleton.

**Dependency wiring (DI graph):**

```
RegionProcessorService
  ├── RegionEvaluator
  │     ├── IRegionRegistry
  │     ├── ICoordinateMapper
  │     └── IRegionPriorityResolver
  └── HandoffResolver
        └── IRegionRegistry   (reads regions for overlap zone info)

RegionManagerService
  └── IRegionRegistry

RegionPersistence
  └── (none — owns file I/O directly)

RegionRegistry
  └── IRegionPersistence   (loads on initialization)
```

**Modules to delete:**

- `LayerTypeCatalogService`
- `CameraZoneLayerEditorService`
- `CameraZoneLayerRepository`
- `EffectiveZoneCompositionService`
- `LayerTypeSettingsStore`
- All layer-related UI in `MainWindow.axaml.cs` (layer dropdown, layer list box, draw-on-grid button, refresh-composition-preview)

**Modules to modify:**

- `BackgroundEstimationEngine` — Add event emission for detected trains (subscribe point for Region Processor). Remove layer-related code.
- `MainWindow.axaml.cs` — Remove layer workspace UI. Add region workspace UI (region list, create/edit/delete buttons, open grid editor). Wire DI at startup.
- `AppSettingsStore` — Keep grid columns/rows settings (used by Region system).
- `CameraZoneBindingStore` — No structural change needed; zone IDs are already used as region grouping keys.

## Testing Decisions

### What Makes a Good Test

All tests should focus on external behavior, not implementation details. Tests verify:

1. **Input-output correctness** — Given a set of regions and detected trains, does the Region Evaluator produce the expected filtered/enriched output?
2. **State transitions** — Does the Handoff Resolver correctly merge tracks when two detections fall within the time window?
3. **Priority resolution** — Given overlapping cell assignments, does the Priority Resolver return the correct winning region?
4. **Coordinate mapping accuracy** — Given process-frame coordinates and grid dimensions, does the mapper return the correct cell indices at boundaries and edges?
5. **Persistence round-trip** — Does saving a region and reloading it produce an equivalent object?

### Modules to Test

1. **Region Model** — Property tests for GridCell bounds validation (column/row within grid dimensions).
2. **Region Priority Resolver** — Table-driven tests for all priority combinations between the 5 region types. Edge case: two regions of the same type on the same cell (same ID wins, or both apply if compatible).
3. **Coordinate Mapper** — Boundary tests: pixel at exact cell boundary, pixel at frame edge, zero-dimension edge cases.
4. **Region Evaluator** — Integration-style tests per region type:
   - EXCLUDE_REGION: train centered in excluded cell → train is filtered from output
   - HIGH_PROBABILITY_RAIL_REGION: train in rail region → confidence boosted by defined factor
   - ENTER_CROSSROAD_REGION: train moves from non-region cell to region cell → transition event emitted
   - EXIT_CROSSROAD_REGION: train moves from region cell to non-region cell → transition event emitted
   - CAMERA_OVERLAP_REGION: train detected in overlap on zone A and zone B within time window → handoff event emitted
5. **Handoff Resolver** — Tests for time window boundary (detections just inside vs. just outside 500ms), confidence averaging, motion state reconciliation.
6. **Region Persistence** — Round-trip test: create regions, save to JSON, reload, assert equivalence. Test with empty region list, single region, multiple zones, CAMERA_OVERLAP_REGION spanning zones.
7. **Grid Editor Dialog** — UI interaction tests: click cell → selected state toggles; save → returns correct cell set; cancel → no changes applied.

### Prior Art in Codebase

The existing test project (`ObjectTracker.UI.Desktop.Tests/`) has ~25 test files covering services, UI projections, and end-to-end scenarios using xUnit. These provide a pattern for:

- Service-level unit tests with mocked dependencies
- Pure function tests (no UI or file I/O)
- Integration-style tests that exercise full service pipelines

New Region tests should follow the same patterns: arrange-test-assert structure, xUnit `[Fact]` and `[Theory]` attributes, and in-memory test fixtures where possible.

**DI testing pattern:**

- Each test creates a `ServiceCollection`, registers only the module under test with mock/fake dependencies (e.g., `FakeRegionPersistence`, `Mock<IRegionRegistry>`), and resolves from an `IServiceProvider`.
- Integration tests register the full pipeline (`CoordinateMapper` → `PriorityResolver` → `Registry` → `Evaluator` → `HandoffResolver` → `ProcessorService`) and exercise end-to-end flows.
- No test creates real file I/O for persistence tests; a `FakeRegionPersistence` in-memory implementation is used instead.

## Out of Scope

1. **PLC ID mapping** — The existing codebase mentions PLC ID as a future feature. Region system does not implement PLC integration.
2. **Train identity persistence** — Local Train ID stability across sessions is out of scope. Region system works with the current per-frame tracking model.
3. **Motion state machine** — Canonical states (`moving`, `stationary`, `uncertain`, `gone-from-track`) are part of Train State but their transition logic is not modified by this PRD. Region system only influences confidence and emits transitions for crossroad regions.
4. **Collision warnings** — Conflict Zone collision detection is mentioned in domain language but not implemented in the current engine. Region system does not add collision logic.
5. **Freeform or polygon region shapes** — Only rectangular cell-based regions are supported. No drag-to-draw, no polygon editing.
6. **Real-time region editing during running Vision Pipeline** — Region changes are saved but not applied until the Vision Pipeline is restarted. Live tuning of region definitions is deferred.
7. **Camera calibration integration** — Region system does not modify or interact with the Calibration process (background baking, rail ROI mask generation).

## Further Notes

### Domain Language Alignment

This PRD uses the existing domain glossary from `CONTEXT.md`:
- **Train** (not "object" or "blob") for tracked LEGO trains
- **Camera Zone** (not "viewport" or "screen") for camera coverage areas
- **Vision Pipeline** (not "engine") for the runtime processing flow
- **Session** (not "installation" or "deployment") for a system run with fixed camera configuration
- **Conflict Zone** for crossing areas where ENTER/EXIT_CROSSROAD_REGION applies
- **Motion State** for the coarse behavioral state of a Train
- **Confidence** for the system's trust level in Train State

### Deleted Code Accounting

The following services and their tests should be removed:
- `LayerTypeCatalogService` (~100 lines)
- `CameraZoneLayerEditorService` (~200 lines)
- `CameraZoneLayerRepository` (~150 lines)
- `EffectiveZoneCompositionService` (~200 lines)
- `LayerTypeSettingsStore` (~100 lines)
- `MotionMaskRefiner` (~100 lines) — related to old layer mask pipeline
- Layer-related UI in `MainWindow.axaml.cs` (~200 lines of the 3400)
- Corresponding tests in `ObjectTracker.UI.Desktop.Tests/`

Total estimated deletion: ~950 lines of code and ~200 lines of tests.

### Estimated New Code Volume

| Module | Estimated Lines |
|--------|----------------|
| Region Model | 80 |
| Region Registry | 150 |
| Region Persistence | 120 |
| Region Priority Resolver | 80 |
| Coordinate Mapper | 60 |
| Region Evaluator | 300 |
| Handoff Resolver | 200 |
| Region Processor Service | 100 |
| Grid Editor Dialog (MVVM) | 250 |
| Region Manager UI Service | 150 |
| Tests | 400 |
| **Total** | **~1,890 lines** |

### Risk Areas

1. **Coordinate mapping accuracy at resolution boundaries** — Scale factor mapping is simple but may produce off-by-one errors at cell boundaries. Unit tests should cover all edge cases (pixel = 0, pixel = width-1, pixel at exact cell boundary).
2. **Thread safety of Region Registry during Vision Pipeline execution** — The Registry is read by the Region Evaluator (subscribed to pipeline events) and written by the UI thread. Use immutable snapshots or lock-free reads.
3. **CAMERA_OVERLAP_REGION handoff timing** — The 500ms time window is a heuristic. It should be configurable and documented with guidance on how to tune it for different track speeds and camera frame rates.
4. **MainWindow.axaml.cs refactoring** — Removing ~200 lines of layer UI from the 3400-line code-behind file will expose hidden dependencies. A separate refactor PR is recommended to extract workspace components into proper MVVM views.
5. **DI container wiring complexity** — The full dependency graph has 8 registered services with cross-references. Misregistration (wrong lifetime, missing interface binding) causes runtime failures that are harder to trace than `null` references. Use a dedicated `ServiceCollection` extension method (`AddRegionServices`) to encapsulate all registrations in one place, and add a startup integration test that resolves every service from the container to verify wiring correctness.
