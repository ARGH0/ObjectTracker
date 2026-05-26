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