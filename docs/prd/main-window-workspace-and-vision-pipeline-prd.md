## Problem Statement

Operators need a clearer main window that separates workspace navigation from runtime control while preserving safety visibility during a Session. The current interface mixes camera, layer, and settings interactions in one dense surface, making it harder to reason about Camera Visibility, Vision Pipeline Inclusion, pending restart behavior, and where Operator Intervention happens when an Ambiguity Alert is active.

## Solution

Redesign the main window around three center workspaces (`Camera`, `Layers`, `Settings`) with two persistent bars: a top global menu bar (`Camera`, `Layers`, `Settings`, `Vision Pipeline`) and a bottom status bar showing runtime indicators only. Keep Vision Pipeline running independently of workspace visibility, use explicit save/confirmation behavior for settings changes, and keep Camera Zone operations and Camera Layer Regions ownership clear by splitting global Layer Type management from camera-specific region authoring.

## User Stories

1. As an operator, I want global menu navigation for `Camera`, `Layers`, and `Settings`, so that I can move between workspaces without losing orientation.
2. As an operator, I want a dedicated `Vision Pipeline` dropdown in the top bar, so that runtime start/stop control is always in one predictable location.
3. As an operator, I want `Start` and `Stop` visible together in the `Vision Pipeline` dropdown, so that available and unavailable actions are explicit.
4. As an operator, I want invalid `Vision Pipeline` actions disabled with clear hints, so that I understand why an action cannot run.
5. As an operator, I want the bottom status bar always visible across all workspaces, so that I can monitor runtime state even when not on camera views.
6. As an operator, I want the bottom status bar to show Vision Pipeline state, Ambiguity Alert state, Calibration state, and pending restart, so that safety-critical status is always visible.
7. As an operator, I want the bottom status indicators to be non-clickable, so that status and navigation responsibilities stay distinct.
8. As an operator, I want to switch from Camera to Layers or Settings while Vision Pipeline continues running, so that I can configure the system without stopping runtime.
9. As an operator, I want navigation to always remain available during Ambiguity Alert, so that I can decide when to return to Camera for Operator Intervention.
10. As an operator, I want the Camera workspace layout to reflow by visible camera count, so that the screen uses available space effectively.
11. As an operator, I want camera tiles ordered by a manually managed camera list order, so that tile positions match my mental model.
12. As an operator, I want camera reorder changes to affect UI presentation only, so that identity mapping and Camera Zone binding remain stable.
13. As an operator, I want a left Camera Panel that can run in `overlay` or `pinned` mode, so that I can choose between temporary and persistent configuration access.
14. As an operator, I want Camera Panel pin state remembered when I return to Camera workspace, so that repeated workflows feel consistent.
15. As an operator, I want a single-select camera list, so that per-camera configuration always targets one clear Camera Zone context.
16. As an operator, I want per-camera controls for Camera Visibility, Debug View, and Vision Pipeline Inclusion, so that I can operate each camera independently.
17. As an operator, I want Camera Visibility and Vision Pipeline Inclusion to be independent, so that display decisions do not silently change processing behavior.
18. As an operator, I want hidden cameras removed from the camera grid while still present in the camera list, so that the grid stays focused but configuration remains accessible.
19. As an operator, I want excluded-but-visible cameras to show raw feed without Train State annotations, so that I do not mistake unprocessed feed for tracked output.
20. As an operator, I want default camera display mode to be live feed with annotations, so that normal monitoring prioritizes operational output.
21. As an operator, I want per-camera Debug View to show the 2x2 debug layer view, so that I can inspect pipeline internals for one camera without changing others.
22. As an operator, I want Debug View state retained while a camera is hidden, so that visibility toggles do not erase my diagnostic setup.
23. As an operator, I want per-camera Debug View state retained for the current Session, so that temporary diagnostics persist during active operations.
24. As an operator, I want camera-level delete actions in the Camera Panel list, so that per-camera destructive actions are scoped where selection context is clear.
25. As an operator, I want delete camera actions to require explicit confirmation, so that accidental removal is prevented.
26. As an operator, I want `Clear Cameras` restricted to when Vision Pipeline is stopped, so that destructive global operations cannot race with runtime processing.
27. As an operator, I want `Clear Cameras` to use clear destructive confirmation messaging, so that high-impact actions are unmistakable.
28. As an operator, I want camera feeds to remain visible as raw live feed when Vision Pipeline is stopped, so that visual monitoring can continue without processed output.
29. As an operator, I want Layers workspace to manage global Layer Types, so that reusable layer categories are configured once.
30. As an operator, I want Layers workspace to show which cameras use each Layer Type, so that dependency impact is visible before changes.
31. As an operator, I want layer usage display to be read-only in Layers workspace, so that camera-specific assignments remain owned by camera workflows.
32. As an operator, I want deletion of an in-use Layer Type blocked, so that Camera Layer Regions are not removed implicitly.
33. As an operator, I want Camera Layer Regions authoring to stay in Camera workspace context, so that region edits remain tied to selected camera context.
34. As an operator, I want Settings workspace changes to require explicit Save, so that incomplete edits do not apply unexpectedly.
35. As an operator, I want `Save / Discard / Cancel` when leaving Settings with unsaved changes, so that I cannot lose or accidentally commit edits.
36. As an operator, I want per-setting inline tags indicating `applies immediately` or `requires Vision Pipeline restart`, so that I understand rollout behavior for each configuration.
37. As an operator, I want pending restart visibility in both Settings and the bottom status bar, so that unapplied saved changes are always obvious.

## Implementation Decisions

- Main window shell introduces stable global chrome with workspace host + top menu + bottom status bar; workspace center replaces content based on selected global workspace.
- Runtime control and workspace navigation are decoupled: Vision Pipeline runs independently from visible workspace.
- `Vision Pipeline` control model uses a dropdown menu with both `Start` and `Stop` visible at all times; action validity is state-based and disabled when invalid.
- Runtime status model is centralized and read-only in the status bar: Vision Pipeline state, Ambiguity Alert state, Calibration state, pending restart marker.
- Camera workspace layout module computes visible tile composition from camera list order + Camera Visibility filter; hidden cameras are omitted from grid.
- Camera list module owns single-select state, manual ordering, camera-level destructive actions, and pin/overlay panel state.
- Camera presentation model supports per-camera display mode selection between default annotated live feed and debug 2x2 diagnostic view.
- Vision Pipeline Inclusion policy is independent from Camera Visibility and applies immediately at runtime.
- Excluded-but-visible camera rendering uses raw feed and suppresses Train State overlays.
- Layers ownership is split into:
  - global Layer Type catalog (create/delete/list precedence and behavior metadata)
  - camera-specific Camera Layer Regions assignments/editing (outside this PRD scope of implementation details)
- Layer dependency reporting module exposes read-only camera usage information for each Layer Type and blocks deletion while in use.
- Settings change management module introduces explicit draft vs saved behavior, unsaved navigation guard, per-setting apply policy metadata, and pending-restart signaling.
- Processing-critical settings are saved but not activated until Vision Pipeline stop/start cycle; immediate settings apply without restart.
- Destructive action policy:
  - camera delete requires confirmation in camera list context
  - clear-all cameras requires Vision Pipeline stopped + explicit destructive confirmation
- Existing domain language is preserved and extended as canonical terms: Vision Pipeline, Camera Visibility, Vision Pipeline Inclusion, Layer Type, Camera Layer Regions.
- Architectural deepening opportunity: introduce a `MainWindowInteractionCoordinator` deep module that centralizes UI behavior state transitions (workspace switches, runtime status, pending restart, destructive guards) behind a small interface consumed by UI event handlers.
- Architectural deepening opportunity: introduce a `CameraWorkspaceProjectionService` deep module that projects camera source state into renderable tile state (visibility, inclusion, mode, order) for deterministic testing.
- Architectural deepening opportunity: introduce a `SettingsApplyPolicyService` deep module that classifies settings into immediate vs restart-required and computes pending-restart state.

## Testing Decisions

- Good tests assert external behavior (visible state, allowed/blocked actions, persisted outcomes, status transitions) rather than control tree internals or private event wiring.
- Module coverage should prioritize deep modules and high-risk safety behavior:
  - MainWindow interaction coordination (workspace switching, unsaved guards, runtime action validity).
  - Camera workspace projection (reflow based on visibility, order stability, excluded rendering mode decisions).
  - Vision Pipeline action policy (start/stop enablement, pending restart signaling, clear-cameras blocked while running).
  - Settings apply policy (inline apply tags, pending restart state derivation).
  - Layer dependency policy (in-use deletion block + usage reporting).
- Prior art for test style already exists in desktop/UI tests for service-focused behavior, including Camera Zone identity, layer catalog and repository persistence, effective composition, and end-to-end reload behaviors.
- Prior art for runtime safety behavior exists in Vision and Desktop tests covering ROI/motion policy, calibration confidence fallback, and runtime cancellation patterns.

## Out of Scope

- Pixel-perfect visual design/theming details beyond behavior and information architecture.
- Full implementation detail of Camera Layer Regions editing workflow in Camera workspace (acknowledged as separate feature flow).
- New Train State detection algorithms or Vision Pipeline model changes unrelated to UI behavior controls.
- Cross-process or distributed Processing Unit orchestration changes.
- Click-through navigation behavior from status bar indicators (status remains non-interactive by design).

## Further Notes

- This PRD aligns with the existing ADR decision on main window workspace and Vision Pipeline behavior and should be treated as the implementation contract for UI behavior.
- Implementation should preserve existing safety posture: Ambiguity Alert blocks Automatic Action semantics without blocking workspace navigation.
- Terminology in UI copy and docs should follow the glossary to avoid drift (`Vision Pipeline`, `Camera Visibility`, `Vision Pipeline Inclusion`, `Layer Type`, `Camera Layer Regions`).
