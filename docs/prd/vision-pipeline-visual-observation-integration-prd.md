# Vision Pipeline Visual Observation Integration PRD

## Problem Statement

Operators expect the Vision Pipeline to use the same Rail ROI-gated, background-diff visual observation behavior that was developed in the existing background estimation flow. Instead, the current Vision Pipeline runtime uses direct full-frame Train Color contour detection. This can produce many small Train Observations from unrelated color blobs across the Source Frame, especially when Debug View is enabled and the operator expects motion-gated evidence.

The current implementation contradicts the snapshot migration intent: Calibration, Rail ROI, moving object extraction, Train Color classification, and Debug Frame production were expected to be adapted into the PipelineSnapshot flow, but the runtime lane path currently bypasses most of that behavior.

## Solution

Move the visual observation behavior out of UI-owned background estimation code and into a Vision-owned module used by the Vision Pipeline lanes. The Vision Pipeline should produce Moving Object Observations and Train Observations from Rail ROI-gated foreground evidence, then pass Train Observations into Train Tracking to produce Train State.

Debug View should show the actual visual evidence phases used to produce Train State, not copied Source Frames or full-frame color blobs. Per-Camera Source runtime processing settings should affect the Vision Pipeline lane that processes that Camera Source.

## User Stories

1. As an operator, I want the Vision Pipeline to use Rail ROI-gated motion evidence, so that off-track changes do not create Train Observations.
2. As an operator, I want Train Color classification to happen inside moving regions, so that static color blobs are not treated as Trains.
3. As an operator, I want one moving Train to produce stable visual evidence, so that Train State does not fragment into many small observations.
4. As an operator, I want Debug View to show the visual observation phases used by the Vision Pipeline, so that I can understand why Train State was produced.
5. As an operator, I want Debug View to include Source Frame, Rail ROI mask, motion mask, moving object evidence, Train Color evidence, and Train Tracking explanation where available, so that I can diagnose each step.
6. As an operator, I want per-Camera Source processing settings to affect the running Vision Pipeline lane after the required apply policy, so that each Camera Zone can be tuned independently.
7. As an operator, I want Threshold to affect foreground extraction, so that lighting and background differences can be tuned per Camera Source.
8. As an operator, I want Motion Area to filter Moving Object Observations, so that tiny foreground noise does not become visual evidence.
9. As an operator, I want Color Min Pixels to filter Train Color classification, so that weak color speckles do not become Train Observations.
10. As an operator, I want Morph Kernel Size to affect motion mask refinement, so that fragmented Train evidence can be connected without excessive noise.
11. As an operator, I want Process Max Width to define the visual observation coordinate space, so that processing load is controlled and annotations line up.
12. As an operator, I want saved Train Color calibration ranges to be used by the Vision Pipeline, so that calibration work affects runtime Train Observations.
13. As an operator, I want visible included Camera Sources to show Annotated Frames from the same visual observation path used for Train Tracking, so that normal operation is truthful.
14. As an operator, I want Debug View changes to apply immediately where safe, so that I can inspect visual evidence without restarting the Vision Pipeline.
15. As an operator, I want calibration-critical changes to require a Vision Pipeline restart, so that Train State safety is not silently changed mid-run.
16. As an operator, I want file-based and USB Camera Sources to use the same visual observation path, so that behavior is consistent across source types.
17. As an operator, I want the Vision Pipeline to skip publishing new snapshots when no fresh Source Frame exists, so that stale frames are not treated as new evidence.
18. As an operator, I want the UI repeat-last-frame behavior to remain a display behavior only, so that repeated visuals do not imply repeated visual evidence.
19. As an integrator, I want Train Observations to be evidence rather than Train State, so that Train Tracking remains responsible for identity continuity and Motion State.
20. As a developer, I want the visual observation logic in a Vision-owned module, so that it can be tested without Avalonia or UI orchestration.
21. As a developer, I want the Vision Pipeline to depend on a visual observation interface rather than UI-owned background estimation code, so that module boundaries are clear.
22. As a developer, I want regression tests proving full-frame static color blobs do not create Train Observations, so that this failure mode does not return.
23. As a developer, I want tests proving Rail ROI and motion-mask settings affect observations, so that runtime tuning behavior is locked down.
24. As a developer, I want Debug Frame tests at the PipelineSnapshot seam, so that UI Debug View consumes the same output as the Vision Pipeline produces.

## Implementation Decisions

- Build a Vision-owned visual observation module that encapsulates foreground extraction, Rail ROI gating, motion mask refinement, Moving Object Observation production, Train Color classification, and Debug Frame production.
- The visual observation module must not depend on UI assemblies or Avalonia controls.
- The Vision Pipeline lane should call the visual observation module for each fresh Source Frame.
- The Vision Pipeline should stop using direct full-frame Train Color contour detection as the default runtime detector.
- Existing full-frame color detection may remain only as a helper or fallback if explicitly needed, but it must not be the main Train Observation source for normal Vision Pipeline operation.
- Moving Object Observations should be produced from refined foreground components inside Rail ROI.
- Train Observations should be produced by classifying dominant Train Color inside Moving Object Observation regions.
- Train Tracking should continue to own Local Train ID continuity, Motion State, Confidence, handoff behavior, and Ambiguity Alerts.
- PipelineSnapshot should continue to carry Source Frame, Annotated Frame, Moving Object Observations, Train Observations, Train States, Debug Frames, and timing metadata.
- Camera Source Status and Vision Pipeline Lane Status remain outside PipelineSnapshot.
- The visual observation module should accept per-Camera Source observation settings containing Threshold, Motion Area, Color Min Pixels, Morph Kernel Size, Process Max Width, and Train Color calibrations.
- The Vision Pipeline lane should receive per-Camera Source desired configuration, not only source identity and Debug View state.
- Calibration/background state should be represented as Vision-owned state that the lane can use without calling UI-owned background estimation code.
- Background baking and sampling behavior may reuse existing calibration services, but the runtime observation path must be Vision-owned.
- Debug Frames should use domain names that describe visual evidence, including Source Frame, Rail ROI mask, motion mask, moving object observation, Train observation, and Train Tracking explanation where available.
- Annotated Frame should draw Train State by default, not raw Moving Object Observations or Train Observations.
- Moving Object Observations and Train Observations should appear in Debug Frames unless they become Train State.
- If no fresh Source Frame is available, the Vision Pipeline lane should not publish a new PipelineSnapshot.
- UI repeat-last-frame remains a tile display policy and must not create new visual evidence.
- Processing-critical or calibration-critical settings should mark pending Vision Pipeline restart according to the existing apply policy.
- Debug View state can apply immediately because it changes output detail, not visual observation safety semantics.

## Testing Decisions

- Good tests should assert externally visible behavior at stable seams: visual observation result, PipelineSnapshot content, and UI frame routing.
- Tests should avoid asserting private OpenCV call order.
- Add visual observation module tests using deterministic synthetic frames and masks.
- Add a test proving a static Train Color blob outside moving Rail ROI evidence does not emit a Train Observation.
- Add a test proving motion inside Rail ROI emits a Moving Object Observation when it exceeds Motion Area.
- Add a test proving motion outside Rail ROI does not emit a Moving Object Observation.
- Add a test proving dominant Train Color inside a moving region emits a Train Observation when it exceeds Color Min Pixels.
- Add a test proving Morph Kernel Size changes fragmented motion connectivity behavior.
- Add a PipelineController test proving the lane uses the visual observation module output rather than direct full-frame color detection.
- Add a PipelineSnapshot test proving Debug Frames include visual observation phases when Debug View is enabled.
- Add a regression test for the observed failure mode where many small full-frame color detections should not become Train Observations.
- Existing prior art includes tests for PipelineController snapshots, lane reconciliation, Debug Frame rendering, Rail ROI mask building, and MotionMaskRefiner behavior.

## Out of Scope

- Redesigning Train Tracking identity continuity.
- Adding PLC ID mapping to PipelineSnapshot.
- Building Automatic Action integration.
- Replacing Train Color calibration UI.
- Building a full whole-track view.
- Persisting every Source Frame, Annotated Frame, or Debug Frame.
- Supporting a separate Train Tracking runtime control.
- Solving all future Rail ROI editor and Safety Margin UX work in this slice.
- Rewriting unrelated UI layout or workspace behavior.

## Further Notes

- This PRD follows the existing domain distinction between Moving Object Observation, Train Observation, Train State, Source Frame, Annotated Frame, and Debug Frames.
- This PRD preserves the ADR direction that the Vision Pipeline orchestrates visual observation lanes and Train Tracking, while Train Tracking remains a distinct internal module.
- The current codebase has a contradiction: completed snapshot-migration issue notes claim Calibration and Rail ROI behavior were preserved in the adapter-first path, but the runtime Vision Pipeline currently bypasses the background-diff/Rail ROI path.
- The recommended first implementation slice is a narrow vertical path: extract motion-gated Train Color classification into a Vision-owned module, wire one Vision Pipeline lane through it, and prove static full-frame color blobs no longer become Train Observations.
