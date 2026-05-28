# PRD: Camera Zone Grid Layering Foundation

## Problem Statement

Operators need a reliable way to define where Train observation is valid, where it must be suppressed, and where caution semantics apply inside each Camera Zone. Today, there is no operator-facing spatial configuration model beyond current calibration and runtime tuning, so region intent is hard to express, hard to persist, and hard to evolve toward Train State safety behaviors.

The immediate need is to establish a grid-based layer and region foundation per Camera Zone that can be edited, persisted, and reloaded across Sessions, while preserving region identity for future eventing and Automatic Action policies.

## Solution

Add a dedicated Camera Zone grid layering experience that sits on top of the selected camera view in a separate workflow. Operators can create multiple layers, assign each layer a layer type, and draw regions on a shared app-wide grid. Region definitions persist per Camera Zone and are restored on startup.

Layer type behavior is governed application-wide: each layer type has numeric precedence (`0` highest), merge policy, and metadata describing whether it is logic-coupled or informational. The first delivery is UI and persistence only; no tracking-pipeline behavior changes are included.

## User Stories

1. As an operator, I want a grid overlay on Camera Zone imagery, so that I can place region boundaries consistently.
2. As an operator, I want the grid dimensions to be configurable once for the application, so that all Camera Zones use a consistent editing resolution.
3. As an operator, I want default grid dimensions, so that I can start quickly without setup.
4. As an operator, I want to edit regions in a separate Camera Zone view, so that runtime processing controls stay uncluttered.
5. As an operator, I want to create multiple layers in a Camera Zone, so that I can separate different spatial intents.
6. As an operator, I want to create multiple layers of the same layer type, so that I can organize regions by operational meaning.
7. As an operator, I want app-wide layer type definitions, so that layer behavior is consistent across Camera Zones.
8. As an operator, I want app-wide numeric precedence for layer types, so that overlap resolution is deterministic.
9. As an operator, I want lower numeric precedence values to win, so that safety semantics are explicit.
10. As an operator, I want per-layer-type merge policy, so that some types can preserve region boundaries while others can collapse for effective masks.
11. As an operator, I want merge policy defaults that preserve regions, so that I keep region identity for future workflows.
12. As an operator, I want to define `No-Vision` regions, so that future tracking can ignore invalid observation areas.
13. As an operator, I want to define `Rail ROI` regions, so that valid Train observation corridors are explicit.
14. As an operator, I want to define `High-Caution` regions, so that safety-sensitive spatial areas are explicit.
15. As an operator, I want to keep `Neutral` coverage available, so that non-specialized zones are representable.
16. As an operator, I want custom layer types, so that future domain-specific semantics can be added without redesigning the editor.
17. As an operator, I want each region to have a stable identity, so that future alerts can target the exact region entered.
18. As an operator, I want each region to have an editable display name, so that I can recognize it quickly.
19. As an operator, I want each region to optionally have a numeric code, so that automation mappings can be added later.
20. As an operator, I want saved Camera Zone regions to reload exactly across Sessions, so that calibration effort is retained.
21. As an operator, I want to bind runtime camera sources to stable Camera Zone identities, so that source changes do not erase region configurations.
22. As an operator, I want new sources to auto-create Camera Zones, so that setup remains fast.
23. As an operator, I want explicit rebind via dropdown, so that I can map a source to an existing Camera Zone intentionally.
24. As an operator, I want layer and region editing without starting automatic processing, so that safety state is unaffected during configuration.
25. As a maintainer, I want layer type policy separated from Camera Zone data, so that app-wide governance remains easy to evolve.
26. As a maintainer, I want deep modules for geometry, overlap, and persistence, so that behavior is testable without UI.
27. As a maintainer, I want schema versioning for new settings files, so that future migrations are safe.
28. As an integrator, I want region identity persistence now, so that later Train region-entry notifications can be introduced without data migration surprises.
29. As an operator, I want confidence that editing one Camera Zone does not affect another, so that zone autonomy is preserved.
30. As a domain owner, I want terms aligned to Camera Zone, Rail ROI, and Session semantics, so that behavior discussions remain consistent.

## Implementation Decisions

- Introduce an application-wide settings model for grid configuration (`columns`, `rows`) with default `32x18`.
- Store application-wide settings in a dedicated app settings file, separate from Camera Zone-scoped settings.
- Introduce stable `CameraZoneId` as the owner key for spatial configuration, distinct from runtime source identifiers.
- Add a source-to-CameraZone binding model with explicit rebind support in UI and auto-create-on-add fallback.
- Introduce an app-wide `LayerTypeDefinition` catalog with:
  - stable `LayerTypeId`
  - display name
  - numeric precedence
  - merge policy (`preserve_regions` or `merge_for_effective_mask`)
  - behavior classification (`logic_coupled` or `informational`)
- Treat layer type precedence as deterministic overlap policy where lower numbers dominate higher numbers.
- Introduce Camera Zone layer model allowing multiple layers for the same layer type.
- Keep per-layer identity and metadata even when merge policy permits effective-mask merging for downstream consumers.
- Introduce region model with immutable `RegionId`, editable `Name`, optional numeric `Code`, geometry expressed in grid coordinates, and required parent layer reference.
- Preserve region boundaries by default to support later region-entry notifications (for example, High-Caution entry by named region).
- Scope first delivery to editor and persistence only; no Automatic Action gating or Train State transitions are changed in this PRD.
- Ensure domain terms in UI and docs follow project glossary (Camera Zone, Rail ROI, Train State, Session).

### Deep Modules

- CameraZoneIdentityService: owns runtime-source to Camera Zone binding, auto-creation policy, and rebind semantics.
- GridGeometryService: owns coordinate transforms between frame pixels and grid cells, plus normalization and bounds enforcement.
- LayerTypePolicyService: owns app-wide layer type definitions, precedence ordering, merge policy interpretation, and validation.
- CameraZoneLayerRepository: owns persistence and retrieval of Camera Zone layers, regions, and schema version migration.
- RegionTopologyService: owns region creation/edit rules, overlap metadata, and per-region identity guarantees.
- EffectiveZoneCompositionService: owns computed overlap/effective-state evaluation for future pipeline consumers without mutating authored layers.

## Testing Decisions

- Good tests assert external behavior (saved settings, overlap outcomes, identity stability, and validation results) rather than UI control internals.
- Test priority modules:
  - CameraZoneIdentityService (auto-create, explicit rebind, stable key behavior)
  - GridGeometryService (cell snapping, bounds clamping, round-trip coordinate fidelity)
  - LayerTypePolicyService (precedence ordering, duplicate precedence rejection, merge policy validation)
  - CameraZoneLayerRepository (persist/reload consistency, schema upgrades, corruption handling)
  - RegionTopologyService (immutable `RegionId`, rename/code edits, geometry updates)
  - EffectiveZoneCompositionService (deterministic overlap resolution from precedence)
- Include integration tests for end-to-end persistence: create Camera Zone layers and regions, restart, reload, verify same authored model.
- Include tests that confirm multiple same-type layers remain distinct when merge policy is `preserve_regions`.
- Reuse prior art from existing settings and mask-related tests: current repository already validates algorithmic behavior via focused module tests in vision and desktop test projects; this feature should follow the same module-first testing style.

## Out of Scope

- Wiring layer output into active detection, Rail ROI gating, or Train State calculations.
- Automatic Action behavior changes based on layer types.
- Region-entry notifications emitted during live tracking.
- Multi-camera identity handoff logic changes.
- Final policy decisions on which layer types are permanently logic-coupled versus informational.
- Full operator workflows for Ambiguity Alert handling tied to new layers.

## Further Notes

- This PRD intentionally establishes a durable spatial-authoring foundation before safety behavior integration.
- Preserving region identity now is a deliberate investment for future High-Caution and similar region-entry eventing.
- App-wide layer type policy plus Camera Zone-local authored layers keeps governance centralized while preserving per-zone autonomy.
