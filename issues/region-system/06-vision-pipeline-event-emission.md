# Issue 6: Vision Pipeline event emission — add detected-train event to BackgroundEstimationEngine

**PRD Reference:** [Region System PRD](../../prd/region-system-prd.md)

## What to build

Extend `BackgroundEstimationEngine` (890 lines) to emit a `TrainDetected` event for each detected train object per frame. This provides the subscription point for the Region Processor Service.

Event payload includes:
- `CameraZoneId` — which camera zone produced this detection
- `LocalTrainId` — the engine's local track ID
- `Position` — bounding box center in process-frame coordinates (x, y)
- `BoundingBox` — width, height in process-frame coordinates
- `TrainColor` — color classification result (if any)
- `Confidence` — current confidence score
- `Timestamp` — frame capture timestamp
- `FrameNumber` — sequential frame counter

Remove all existing layer-related code from the engine while adding this functionality. The event is raised after contour detection and color classification, before track state update.

## Acceptance criteria

- [ ] `TrainDetected` event fires for each detected train per frame
- [ ] Event payload includes all required fields (zoneId, localId, position, boundingBox, color, confidence, timestamp)
- [ ] Position is in process-frame coordinates (not original frame coordinates)
- [ ] Event raised after contour detection and color classification, before track update
- [ ] All layer-related code removed from BackgroundEstimationEngine
- [ ] Unit tests verify event fires with correct payload on detected trains
- [ ] Unit tests verify event does NOT fire on frames with no detections
- [ ] Integration test: subscribe to event, run engine on test video, assert events received

## Blocked by

- #3 (Registry), #5 (Mapper + Resolver)
