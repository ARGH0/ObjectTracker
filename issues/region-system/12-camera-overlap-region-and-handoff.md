# Issue 12: CAMERA_OVERLAP_REGION + Handoff Resolver — complete vertical slice (cross-camera track merging)

**PRD Reference:** [Region System PRD](../../prd/region-system-prd.md)

## What to build

Complete end-to-end implementation of CAMERA_OVERLAP_REGION behavior and the Handoff Resolver. Builds on all previous region flows (#8-11).

Flow:
1. Operator creates CAMERA_OVERLAP_REGION via Grid Editor Dialog, selects cells in overlapping area visible from multiple cameras
2. Region saved with `overlappingZoneIds` array (e.g., `["zone-a", "zone-b"]`)
3. Region loaded into Registry, associated with both zones
4. Camera A emits `TrainDetected` event for train in overlap cell
5. Camera B emits `TrainDetected` event for train in same physical overlap cell (within configurable time window, default 500ms)
6. Handoff Resolver matches the two detections using object signature: Train Color, position within overlap, timestamp proximity
7. On match: emit `TrackHandoff` event with source zone, target zone, merged Local Train ID, confidence averaging, motion state reconciliation
8. Duplicate detection on target zone is suppressed

The Handoff Resolver maintains state for pending handoffs within the time window. It matches detections across zones and merges tracks into a unified view.

## Acceptance criteria

- [ ] Operator can create CAMERA_OVERLAP_REGION with `overlappingZoneIds` via Grid Editor Dialog
- [ ] Region persists with overlapping zone references and loads correctly
- [ ] Train detected in overlap on Camera A is tracked normally
- [ ] Train detected in same overlap on Camera B within 500ms triggers handoff matching
- [ ] Handoff Resolver matches detections using: Train Color, position within overlap, timestamp proximity
- [ ] On match: `TrackHandoff` event emitted with source zone, target zone, merged Local Train ID
- [ ] Duplicate detection on target zone suppressed after handoff
- [ ] Confidence averaged from both zones in merged track
- [ ] Motion state reconciled from both zones (e.g., moving + stationary → moving)
- [ ] Time window boundary: detections just inside 500ms trigger handoff, just outside do not
- [ ] CAMERA_OVERLAP_REGION has priority 30 (lowest) — informational, does not suppress other behaviors
- [ ] Unit tests verify handoff matching logic with mocked multi-zone detections
- [ ] Unit tests verify time window boundary conditions
- [ ] Integration test: full flow from dual-camera detection to merged track

## Blocked by

- #11 (EXIT_CROSSROAD_REGION)
