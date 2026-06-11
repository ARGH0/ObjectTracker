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
The identifier assigned to a Train within one processing unit. It only needs to be stable inside that unit's scope.
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

**Motion State**:
The coarse behavioral state of a Train. Canonical states are `moving`, `stationary`, `uncertain`, and `gone-from-track`.
_Avoid_: Presence flag, active flag

**Confidence**:
The system's trust level in the current Train State. Low confidence does not erase the Train; it marks the state as unsafe for automatic action.
_Avoid_: Certainty, health

**Vision Pipeline**:
The runtime flow that produces Train State from camera input during a Session. It can be running or stopped; configuration changes may be saved but not applied until the Vision Pipeline is restarted.
_Avoid_: Engine, processing unit (when referring to start/stop runtime control)

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

**Enter Region**:
A Camera Layer Region whose Layer Type marks where a Train enters a monitored route segment or Conflict Zone. Its name is the operator-defined automation identifier for that region.
_Avoid_: PLC trigger, entry blob, anonymous entry area

**Exit Region**:
A Camera Layer Region whose Layer Type marks where a Train leaves a monitored route segment or Conflict Zone. Its name is the operator-defined automation identifier for that region.
_Avoid_: PLC trigger, exit blob, anonymous exit area

**Processing Unit**:
The runtime boundary that processes one or more camera zones and owns Local Train IDs within that boundary.
_Avoid_: Node, host
