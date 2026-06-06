# Vision Pipeline Snapshot Frame Feed PRD

## Problem Statement

Operators expect Object Tracker to follow one coherent runtime flow: a Camera Source provides frames, the Vision Pipeline processes those frames, Train Tracking maintains Train State, and the UI displays the resulting Annotated Frame or Debug Frames. The current code does not consistently follow that flow. The UI Start/Stop path runs a separate processing module, visible included camera tiles can still use raw preview behavior, `LiveAnnotated` can be a label rather than a true Annotated Frame feed, and the existing single-source Vision Pipeline snapshot path is not the active UI frame feed.

This creates coupling and low locality. Camera Source ownership, frame extraction, Calibration, annotations, debug output, status, and selected-camera processing are spread across UI orchestration and Vision modules. It is hard to test the real operator flow through one stable interface, and hard for future agents to understand where frame output, Train State, and status are meant to come from.

## Solution

Make `PipelineSnapshot` the UI frame feed for every visible Camera Source included in the Vision Pipeline. A `PipelineSnapshot` is produced per Camera Source, contains the Source Frame, Annotated Frame, optional Debug Frames, observations, relevant Train States, and frame timing metadata, and is routed by Camera Source ID to the UI.

The Vision Pipeline remains the single operator-controlled runtime. Internally, it coordinates per-Camera Source visual observation lanes and a distinct Train Tracking module. Visual observation lanes produce Moving Object Observations and Train Observations. Train Tracking turns observations across Camera Sources into Train State with stable Local Train IDs inside one Processing Unit. Snapshot assembly produces per-Camera Source output for the UI.

Camera Source Status, Vision Pipeline Lane Status, global Vision Pipeline status, and Ambiguity Alert state stay outside `PipelineSnapshot`. They are separate typed status flows so frame output does not become the source of truth for runtime health or safety intervention.

## User Stories

1. As an operator, I want `LiveAnnotated` camera tiles to show true Annotated Frames from the Vision Pipeline, so that the tile label matches the runtime behavior.
2. As an operator, I want Debug View to show Debug Frames from the same Vision Pipeline flow as LiveAnnotated, so that debug evidence explains the current Train State.
3. As an operator, I want visible and included Camera Sources to display Vision Pipeline output while the Vision Pipeline is running, so that I see what automatic processing is using.
4. As an operator, I want visible and excluded Camera Sources to show raw Camera Source feed, so that I can monitor a Camera Source without contributing it to the Vision Pipeline.
5. As an operator, I want hidden and included Camera Sources to keep contributing to Train State, so that Camera Visibility does not accidentally disable processing.
6. As an operator, I want hidden and excluded Camera Sources to use no UI tile or Vision Pipeline lane, so that unused Camera Sources do not consume work.
7. As an operator, I want visible and included Camera Sources to show raw Camera Source feed when the Vision Pipeline is stopped, so that I retain camera visibility during setup.
8. As an operator, I want visible and included Camera Sources to switch to a starting placeholder when the Vision Pipeline starts, so that the tile does not pretend raw feed is annotated output.
9. As an operator, I want visible tiles to switch immediately to raw feed when a Camera Source is excluded from the Vision Pipeline, so that the tile mode remains truthful.
10. As an operator, I want Debug View changes to apply immediately, so that I can inspect processing without restarting the Vision Pipeline.
11. As an operator, I want Vision Pipeline Inclusion changes to start or stop only the affected Camera Source lane, so that other Camera Sources keep running.
12. As an operator, I want the Vision Pipeline to process all included Camera Sources, so that processing is not limited to the selected Camera Source.
13. As an operator, I want one global Vision Pipeline start/stop control, so that I do not manage separate runtimes per Camera Source.
14. As an operator, I want Camera Source Status and Vision Pipeline Lane Status to be visually distinct, so that I can tell feed failures from processing failures.
15. As an operator, I want tile overlays to show stale or failed status while preserving last visual context when configured, so that I can diagnose problems without losing evidence.
16. As an operator, I want the Camera Panel to show detailed Camera Source Status and Vision Pipeline Lane Status for the selected Camera Source, so that I can inspect hidden or visible Camera Sources.
17. As an operator, I want hidden included Camera Sources to show status in the Camera Panel or camera list, so that failures are visible even without a tile.
18. As an operator, I want the bottom status bar to remain global and indicator-only, so that per-Camera Source details do not overwhelm the main runtime status.
19. As an operator, I want Camera Source Status states to be starting, running, stale, failed, and stopped, so that feed state language is consistent.
20. As an operator, I want Vision Pipeline Lane Status states to be starting, running, stale, failed, and stopped, so that processing state language is consistent.
21. As an operator, I want a Camera Source lane failure to leave other lanes running, so that one failed Camera Zone does not stop the whole Vision Pipeline.
22. As an operator, I want Ambiguity Alerts to be raised only when Train identity or state becomes unsafe, so that feed failures and safety intervention stay distinct.
23. As an operator, I want a global Vision Pipeline target FPS setting, so that I can tune processing load for the running hardware.
24. As an operator, I want target FPS changes to apply immediately, so that I can tune performance during a Session without resetting Train State.
25. As an operator, I want actual processed FPS reported per Camera Source lane, so that I can see whether the hardware is keeping up.
26. As an operator, I want UI missing-frame behavior to be configurable between repeat-last-frame and black-frame, so that the display matches my operating preference.
27. As an operator, I want repeat-last-frame to be the default missing-frame behavior, so that brief gaps do not remove useful visual context.
28. As an operator, I want stale and failed overlays to appear regardless of missing-frame behavior, so that old frames are never mistaken for fresh output.
29. As an operator, I want file-based Camera Sources to be a single loopable video source, so that a Camera Source represents one Camera Zone feed rather than a playlist.
30. As an operator, I want file-based Camera Source loop behavior to be per source, so that each file feed behaves independently.
31. As an operator, I want USB and file-based Camera Sources both supported in the new flow, so that the migration does not leave one source type behind.
32. As an operator, I want session-owned Camera Source feeds for USB and file-based sources, so that raw feed and Vision Pipeline consumption share one feed timeline.
33. As an operator, I want a live USB Camera Source to have one owning feed, so that multiple consumers do not halt or compete for the physical camera.
34. As an operator, I want a file-based Camera Source to have one playback feed, so that raw display and processing do not advance separate timelines.
35. As an operator, I want Source Frames in snapshots, so that future tools can compare unannotated and annotated views without opening a separate feed.
36. As an operator, I want Source Frames aligned to Vision Pipeline coordinate space, so that image positions and annotations line up.
37. As an operator, I want Annotated Frames to show Train State by default, so that the normal view focuses on operator-relevant state.
38. As an operator, I want Moving Object Observations shown only through Debug Frames for now, so that normal operation is not cluttered by unconfirmed evidence.
39. As an operator, I want Train Observations shown only through Debug Frames unless they become Train State, so that observations do not imply stable identity.
40. As an operator, I want Debug Frames to include visual observation phases and Train Tracking explanation when enabled, so that I can understand how Train State was produced.
41. As an operator, I want Train State to continue across frames even when no new observation appears, so that stationary or temporarily hidden Trains remain visible as state.
42. As an operator, I want Local Train IDs to remain stable as a Train moves between Camera Zones inside one Processing Unit, so that I can follow the same Train across the track.
43. As an operator, I want the same Local Train ID allowed briefly in multiple Camera Source snapshots during handoff, so that overlap or transition does not immediately create an alert.
44. As an operator, I want duplicate Local Train ID presence beyond the handoff grace period to raise an Ambiguity Alert, so that impossible identity state is not silently trusted.
45. As an operator, I want PLC ID mapping to stay outside the snapshot feed, so that operator-approved downstream identity remains separate from visual frame processing.
46. As a developer, I want one snapshot output seam for UI frames, so that tests exercise the same interface as the UI.
47. As a developer, I want typed runtime status flows, so that tests assert stable state values rather than parsing text logs.
48. As a developer, I want `PreviewFrameSet` removed from the UI seam, so that Debug Frames replace the old callback-specific output shape.
49. As a developer, I want Train Tracking to be a distinct internal module, so that train identity continuity is not mixed into per-Camera Source visual observation lanes.
50. As a developer, I want the Vision Pipeline to reconcile desired lane configuration, so that start, stop, inclusion changes, Debug View changes, and target FPS changes use one lifecycle model.
51. As a developer, I want restart-required settings tracked per lane internally, so that pending restart state has locality even when the bottom bar summarizes globally.
52. As a developer, I want processing-critical changes to mark pending restart instead of silently applying, so that Calibration and Train State safety are preserved.
53. As a developer, I want immediate-safe settings to apply without restart, so that Debug View and target FPS changes are low-friction.
54. As a developer, I want `PipelineSnapshot` to remain status-free, so that frame output does not become a runtime health object.
55. As a developer, I want Ambiguity Alert state outside `PipelineSnapshot`, so that safety intervention can account for whole Processing Unit state.
56. As a developer, I want Train State updates published through `PipelineSnapshot` for now, so that the first migration does not introduce a second Train State output seam.
57. As a developer, I want future Automatic Action integration to justify its own seam later, so that the current work stays focused on UI frame flow.
58. As a developer, I want video playlist removal completed before snapshot migration, so that Camera Source feed ownership is simpler.
59. As a developer, I want an adapter-first migration, so that USB and video can enter the new snapshot flow without rewriting every processing implementation at once.
60. As a developer, I want existing behavior covered by regression tests, so that UI flow changes do not break Camera Visibility, Vision Pipeline Inclusion, Debug View, or USB ownership semantics.

## Implementation Decisions

- The Vision Pipeline is the single operator-controlled runtime. It starts and stops as one runtime, while internally owning multiple processing lanes for included Camera Sources.
- Each included Camera Source has a Vision Pipeline lane. Lanes run concurrently so one stale or failed Camera Source does not block other lanes.
- Vision Pipeline Inclusion is independent from Camera Visibility. Inclusion controls whether a Camera Source contributes to the Vision Pipeline; visibility controls whether a tile is shown.
- `PipelineSnapshot` is per Camera Source, not a multi-camera batch.
- `PipelineSnapshot` routing uses Camera Source ID. Camera Zone context may be included for display and interpretation, but the frame producer identity is the Camera Source.
- `PipelineSnapshot` carries Source Frame, Annotated Frame, optional Debug Frames, Moving Object Observations, Train Observations, relevant Train States, and frame timing metadata.
- `PipelineSnapshot` never carries Camera Source Status, Vision Pipeline Lane Status, global Vision Pipeline status, failure messages, restart eligibility, or Ambiguity Alert state.
- Source Frame is the unannotated frame in the Vision Pipeline coordinate space. It is not necessarily the original camera bytes.
- Annotated Frame is the default operator-facing frame for included Camera Sources when Debug View is not shown.
- Debug Frames are named phase frames produced only when Debug View is enabled for the Camera Source.
- Initial Debug Frame kinds should include Rail ROI mask, motion mask, moving color, Train Color observation, motion overlay, and Train Tracking explanation where available.
- Annotated Frame draws Train State by default. Moving Object Observations and Train Observations are shown through Debug Frames for now.
- Moving Object Observations and Train Observations replace generic detection language in the target snapshot model.
- Train State remains the reported domain output. Observations are evidence, not Train State.
- Train Tracking is a distinct internal module under the Vision Pipeline runtime. It turns observations from one or more Camera Sources into Train State with stable Local Train IDs inside a Processing Unit.
- Visual observation lanes produce evidence and Debug Frames. Train Tracking owns identity continuity, Confidence, Motion State, handoff, and Local Train ID stability.
- PipelineSnapshot still carries Train State because it is the UI frame feed of the overall Vision Pipeline runtime, not only the visual observation lane.
- Per-Camera Source PipelineSnapshot Train States are the states relevant to that Camera Source or Camera Zone. Whole Processing Unit Train Tracking state is not embedded in every snapshot.
- The same Local Train ID may appear in multiple Camera Source snapshots during handoff or overlap for a short global grace period.
- If the same Local Train ID appears in multiple Camera Source snapshots beyond the handoff grace period and cannot be explained, Train Tracking raises an Ambiguity Alert through the separate safety/intervention flow.
- PLC ID mapping stays outside `PipelineSnapshot`. Train State in snapshots uses Local Train IDs.
- Camera Source Status and Vision Pipeline Lane Status are separate typed status flows. Both use starting, running, stale, failed, and stopped as state names, but the types remain distinct.
- Camera Source Status describes the feed. Vision Pipeline Lane Status describes processing for one included Camera Source.
- A lane is stale when it is expected to process but has not produced a fresh PipelineSnapshot within the lane freshness window.
- If a lane is stale or failed, the UI keeps showing the last snapshot when configured to repeat last frame, with a status overlay. If no snapshot ever arrived, the UI shows a placeholder or black frame.
- Debug View stale behavior keeps showing the last full Debug Frames if available, with stale or failed overlay.
- Runtime log text remains free-form, but UI state is driven by typed status objects.
- Visible and included Camera Sources use PipelineSnapshot for tile images while the Vision Pipeline is running. The UI does not run a separate raw tile preview consumer for those tiles.
- Visible and excluded Camera Sources use raw Camera Source feed outside the Vision Pipeline and do not produce PipelineSnapshots.
- When the Vision Pipeline is stopped, visible Camera Sources show raw Camera Source feed regardless of inclusion, with stopped state shown separately.
- When the Vision Pipeline starts, included visible tiles switch immediately to snapshot display mode and show a starting placeholder until the first PipelineSnapshot arrives.
- Stopping the Vision Pipeline clears displayed snapshots and switches visible tiles back to raw Camera Source feed.
- One global Vision Pipeline target FPS applies to all running lanes initially. It applies immediately and does not mark pending restart.
- Each lane processes the latest available Source Frame at the global target FPS. It does not process a backlog of stale frames.
- Runtime file-based Camera Sources follow the same latest-frame-at-timed-interval rule as USB. Deterministic every-frame processing belongs to tests or offline tools.
- `PipelineSnapshot.FramesPerSecond` means actual processed FPS for the Camera Source lane, not configured target FPS.
- UI render cadence is separate from Camera Source FPS and Vision Pipeline target FPS. The UI keeps the latest snapshot per Camera Source and may drop older unrendered snapshots.
- Global UI missing-frame behavior has two modes: repeat-last-frame and black-frame. The default is repeat-last-frame. Status overlays always apply.
- File-based Camera Sources are a single loopable video source, not a playlist. A file-based Camera Source has one video path and a per-source loop setting.
- Video playlist removal should be done before the snapshot-flow migration.
- USB and file-based Camera Sources should both be supported by the first user-visible snapshot-flow slice.
- Camera Source feeds are session-owned for both USB and file-based sources. Raw display and Vision Pipeline lanes consume the same feed rather than opening separate capture/playback timelines.
- The current single-source start shape is replaced by start/reconcile/stop behavior for all included Camera Sources.
- Session or UI state owns desired Camera Source inclusion and lane configuration. Vision Pipeline owns active lane lifecycle that reconciles to desired state.
- Desired lane configuration includes Debug View state and processing-relevant settings.
- Debug View and target FPS apply immediately. Calibration-critical or processing-critical changes mark pending restart according to apply policy.
- Pending restart state is tracked per lane internally and summarized globally in the bottom status bar when any lane has pending restart changes.
- Camera Panel shows detailed Camera Source Status and Vision Pipeline Lane Status for the selected Camera Source. Tile overlays show compact state for visible Camera Sources. Bottom bar stays global.
- Session audit logging records operator actions, runtime state changes, status failures, Ambiguity Alerts, and Operator Interventions, but not every PipelineSnapshot by default.
- Persisting or recording snapshots is out of the default flow and should be handled by a future output adapter if needed.
- Adapter-first migration is preferred. Existing processing implementation may be wrapped internally while the UI seam moves to PipelineSnapshot. Transitional types must not remain as public UI frame seams.

## Testing Decisions

- Good tests should verify behavior through stable interfaces: Camera Source feed behavior, Vision Pipeline lane reconciliation, typed status flows, PipelineSnapshot output, and UI projection behavior. Tests should not assert private helper ordering or UI event wiring when a projection or module interface can express the same behavior.
- The PipelineSnapshot output seam should be tested with recording adapters that capture emitted snapshots per Camera Source.
- Vision Pipeline lane reconciliation should be tested by changing desired inclusion, Debug View state, target FPS, and stop/start state, then asserting lane status and snapshot output.
- Camera Source feed ownership should be tested with fake USB and file-based feed adapters so tests do not require physical cameras or real video timing.
- File-based Camera Source tests should prove one video source plus loop/no-loop behavior, and prove playlists no longer drive runtime processing.
- UI projection tests should prove visible/included/running tiles use PipelineSnapshot, visible/included/stopped tiles use raw feed, visible/excluded tiles use raw feed, hidden/included Camera Sources have no tile, and Debug View mode renders Debug Frames.
- Typed status tests should prove Camera Source Status and Vision Pipeline Lane Status are distinct, use the same canonical state names, and never appear in PipelineSnapshot.
- Stale/failed UI behavior should be tested for repeat-last-frame, black-frame, no-frame-yet placeholder, Annotated Frame, and Debug Frames.
- Train Tracking tests should prove Local Train ID continuity across Camera Sources inside one Processing Unit, allowed handoff overlap grace period, and Ambiguity Alert when duplicate presence exceeds the grace period.
- Snapshot content tests should prove Source Frame and Annotated Frame share coordinate space, Debug Frames are present only when Debug View is enabled, and observations are separate from Train State.
- Target FPS tests should prove global target FPS applies immediately, actual FPS is measured separately, and lanes process latest available Source Frames rather than a backlog.
- Prior test patterns already exist for camera tile feed coordination, camera grid projection, Camera Source status projection, USB owner management, USB processing through shared feed, MainWindow end-to-end regression behavior, Rail ROI mask building, motion mask refinement, and BackgroundEstimationEngine cancellation. New tests should follow those patterns by favoring small deterministic modules and fake adapters.
- Modules that should have direct tests include the session-owned Camera Source feed module, file-based Camera Source feed module, Vision Pipeline lane reconciler, PipelineSnapshot output adapter, typed status projections, Debug Frame production policy, Train Tracking handoff policy, and UI tile display projection.
- Integration-style regression tests should cover the full operator flow from Camera Source setup through Vision Pipeline start, snapshot rendering, Debug View toggle, inclusion change, stale/failure status, and Vision Pipeline stop.

## Out of Scope

- Implementing downstream Automatic Action or PLC control.
- Embedding PLC ID mapping in PipelineSnapshot.
- Persisting every PipelineSnapshot, Source Frame, Annotated Frame, or Debug Frame.
- Building a full whole-track view or separate Train Tracking UI.
- Adding separate Train Tracking start/stop controls.
- Modeling Camera Zone overlap in Camera Layer Regions for this iteration.
- Per-Camera Zone pair handoff grace periods.
- Per-lane Vision Pipeline target FPS.
- Showing Source Frame as a third primary tile mode.
- Showing Moving Object Observations or Train Observations on Annotated Frame by default.
- Rewriting all vision internals in one step before unifying the UI frame seam.
- Supporting video playlists after the file-based Camera Source simplification.

## Further Notes

- This PRD follows ADR-0004 by keeping Camera Visibility independent from Vision Pipeline Inclusion, keeping the bottom bar global, and allowing Debug View and inclusion changes to apply immediately.
- This PRD follows ADR-0005 by keeping live USB Camera Sources session-owned and shared across consumers.
- This PRD follows ADR-0006 by keeping Train Tracking distinct inside the Vision Pipeline runtime rather than collapsing identity continuity into per-Camera Source visual observation lanes.
- The strongest first deepening opportunity is the PipelineSnapshot seam: UI frame rendering, Debug View rendering, tests, and future output adapters should all exercise that seam rather than callbacks or duplicate raw preview paths.
