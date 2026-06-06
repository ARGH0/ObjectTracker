# Object Tracker

Object Tracker identifies and follows LEGO trains moving through camera-covered rail layouts. Its language centers on train identity, uncertainty handling, operator intervention, and camera-bounded observation rather than generic computer-vision terms.

## Language

### Trains And Identity

**Train**:
A physical LEGO train, including its engine and attached wagons, treated as one tracked object for identity and motion.
_Avoid_: Wagon, carriage set, blob

**Train Color**:
The operator-recognized visual identity of a Train, currently expected to be unique within a deployment, such as Red, Green, Blue, or White.
_Avoid_: Appearance class, label

**Local Train ID**:
The identifier assigned to a Train within one Processing Unit. It is shared across that Processing Unit's Camera Sources and should remain stable as a Train moves between Camera Zones, but it does not need to match Local Train IDs used by other Processing Units.
_Avoid_: Global ID, universal ID

**PLC ID**:
The operator-approved external identity that downstream automation uses for a Train. Different processing units may use different Local Train IDs as long as they map to the correct PLC ID.
_Avoid_: Local ID, tracker ID

### Observation And State

**Session**:
A run of the system with a fixed camera position and angle, including its calibration, masks, thresholds, and audit log.
_Avoid_: Installation, deployment

**Calibration**:
The session-start process that aligns the camera view, validates the rail mask, and prepares the current lighting and background baseline.
_Avoid_: Setup, bootstrapping

**Train State**:
The reported state of a Train on each update: identity, color, image position, direction, speed, confidence, motion state, and collision warning state.
_Avoid_: Detection, blob output

**Train Tracking**:
The continuity process that turns observations from one or more Camera Sources into Train State with stable Local Train IDs inside a Processing Unit.
_Avoid_: Detection pipeline, vision logic, tracker ID assignment

**Motion State**:
The coarse behavioral state of a Train. Canonical states are `moving`, `stationary`, `uncertain`, and `gone-from-track`.
_Avoid_: Presence flag, active flag

**Confidence**:
The system's trust level in the current Train State. Low confidence does not erase the Train; it marks the state as unsafe for automatic action.
_Avoid_: Certainty, health

**Vision Pipeline**:
The runtime flow that produces Train State from camera input during a Session. It can be running or stopped; configuration changes may be saved but not applied until the Vision Pipeline is restarted.
_Avoid_: Engine, processing unit (when referring to start/stop runtime control)

**Debug Frames**:
Intermediate visual outputs from the Vision Pipeline used to explain how a Train State was produced. Debug Frames are for operator and developer inspection, not separate Train State.
_Avoid_: Preview frames, diagnostic images, intermediate phase frames

**Source Frame**:
The unannotated frame from a Camera Source in the coordinate space used by the Vision Pipeline for Train State and annotations. It may reflect capture normalization and should not be assumed to be the original camera bytes.
_Avoid_: Raw frame, unprocessed frame, unannotated frame

**Annotated Frame**:
The operator-facing frame produced by the Vision Pipeline with visual annotations for the current Train State. It is the default visual output for included Camera Sources when Debug View is not shown.
_Avoid_: Processed frame, rendered frame, output frame

**Moving Object Observation**:
Visual evidence that something is moving in a Source Frame, before it has been identified as a Train. It may explain later Train State, but it is not itself Train State.
_Avoid_: Moving object detection, blob, motion hit

**Train Observation**:
Visual evidence that a Train or Train Color is present in a Source Frame. It contributes to Train State but does not by itself decide identity continuity, Motion State, or safety.
_Avoid_: Train detection, appearance detection, label

**Vision Pipeline Lane Status**:
The runtime state of Vision Pipeline processing for one included Camera Source. It is separate from processed frame output and can report states such as starting, running, stale, failed, or stopped for that Camera Source's processing lane.
_Avoid_: Camera health, snapshot status, pipeline frame state

### Layout And Coverage

**Rail ROI**:
The region of interest that bounds where valid train motion may appear in a camera view.
_Avoid_: Full-frame mask, scene mask

**Conflict Zone**:
An area of the layout where train movement is safety-critical or ambiguous, such as crossings, switches, or the bridge underpass.
_Avoid_: Hotspot, special area

**Camera Zone**:
The portion of the rail layout observed by one camera.
_Avoid_: Screen, viewport

**Camera Source**:
The physical or file-based feed that provides images for one Camera Zone during a Session. A live physical camera source should have one owning feed even when multiple parts of the system view or process it.
_Avoid_: Stream, playback, device handle

**Camera Source Status**:
The runtime state of a Camera Source feed, such as starting, running, stale, failed, or stopped. It is separate from Vision Pipeline Lane Status and is not part of Train State or processed frame output.
_Avoid_: Pipeline status, snapshot status, tile status

**Camera Visibility**:
Whether a camera tile is shown in the Camera workspace. Canonical states are `visible` and `hidden`.
_Avoid_: Enabled view, display toggle

**Vision Pipeline Inclusion**:
Whether a camera source contributes frames to the Vision Pipeline. Canonical states are `included` and `excluded`. This is independent from Camera Visibility.
_Avoid_: Include in processing, active camera flag

**Layer Type**:
A globally defined layer category that can be reused across cameras.
_Avoid_: Per-camera layer definition

**Camera Layer Regions**:
The camera-specific region drawings and assignments that use global Layer Types within one Camera Zone.
_Avoid_: Global region map

**Processing Unit**:
The runtime boundary that processes one or more camera zones and owns Local Train IDs within that boundary.
_Avoid_: Node, host

### Safety And Intervention

**Automatic Action**:
Any downstream system behavior taken without immediate operator input, based on reported Train State.
_Avoid_: Autonomous decision, automatic control

**Operator Intervention**:
A manual resolution of ambiguity or mapping problems, such as marking a Train removed from track or reconnecting it to the correct identity.
_Avoid_: Override, manual fix

**Ambiguity Alert**:
A blocking alert raised when Train identity or state is unsafe to trust. It remains active until an Operator Intervention resolves it.
_Avoid_: Warning, notification

## Flagged Ambiguities

- **Train vs wagon**: The system tracks whole Trains, not individual wagons.
- **Identity continuity**: Local Train IDs may differ between Processing Units, but PLC ID mapping must remain correct.
- **Gone-from-track vs uncertain**: `uncertain` is a temporary low-confidence state; `gone-from-track` is the safety fallback after the allowed uncertainty window expires.

## Example Dialogue

Developer: "If the blue train stops under the bridge, do we still track it?"

Domain expert: "Yes. It remains the same Train with the same PLC ID, but its Motion State may become `stationary` and its Confidence may drop if the bridge hides it."

Developer: "When do we treat it as gone from track?"

Domain expert: "Only after the uncertainty window expires and no Operator Intervention has resolved the Ambiguity Alert."

Developer: "If another camera sees it later, does it keep the same Local Train ID?"

Domain expert: "Not necessarily. The destination Processing Unit can assign a different Local Train ID, but the operator-configured PLC ID must still be correct."
