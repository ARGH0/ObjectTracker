## Problem Statement

Operators can add multiple USB Camera Sources, but visible live feeds halt when another USB camera is added. Video file camera sources continue playing correctly because they can be opened independently, while live physical USB devices are fragile when multiple parts of the app open the same device through separate capture handles. This makes multi-camera layout observation unreliable during a Session and undermines confidence that visible Camera Zones are still being watched.

## Solution

Introduce session-owned USB Camera Source feed ownership. Each live physical USB Camera Source has one owning feed during a Session, and camera tiles, calibration/background sampling, and selected-camera Vision Pipeline processing consume immutable latest-frame snapshots from that owner instead of opening the device directly. Camera tile consumers are diffed so adding one USB camera only starts the new owner/consumer and does not restart existing visible USB feeds. USB capture settings are configurable per Camera Source in the selected Camera Panel, with explicit Apply/Revert behavior, per-camera runtime status, and a targeted Restart Camera Source action.

## User Stories

1. As an operator, I want multiple visible USB Camera Sources to keep showing live feeds at the same time, so that I can monitor several Camera Zones during one Session.
2. As an operator, I want adding a second USB Camera Source to leave the first USB feed uninterrupted, so that setup work does not break existing monitoring.
3. As an operator, I want adding a third USB Camera Source to start only that new source, so that existing camera tiles do not flicker, halt, or reopen.
4. As an operator, I want video file camera playback to keep working as it does today, so that the USB fix does not regress file-based testing workflows.
5. As an operator, I want a visible USB camera that is excluded from the Vision Pipeline to still show raw live feed, so that display and processing decisions stay independent.
6. As an operator, I want a hidden USB camera that is still included in the Vision Pipeline to remain available to processing, so that hiding a tile does not silently stop processing.
7. As an operator, I want a hidden and excluded USB camera to release its physical device after a short delay, so that unused devices are not kept open unnecessarily.
8. As an operator, I want Camera Visibility changes to affect live viewing immediately, so that I can control the camera grid without restarting the app.
9. As an operator, I want USB Camera Source ownership to follow the physical source identity, so that Camera Zone rebinding does not accidentally create duplicate device owners.
10. As an operator, I want duplicate USB Camera Sources to be blocked, so that the same physical camera is not opened twice.
11. As an operator, I want already-added USB cameras to appear as disabled entries in the add-camera dialog, so that I understand they were detected but cannot be added again.
12. As an operator, I want USB discovery to avoid probing already-owned cameras, so that discovery does not disrupt live feeds.
13. As an operator, I want USB Camera Sources to start only when needed by visibility or processing, so that hidden and excluded sources do not consume hardware resources.
14. As an operator, I want all USB Camera Source owners to stop when the Session ends or cameras are cleared, so that physical devices are released cleanly.
15. As an operator, I want visible USB tiles to render from the latest successful frame, so that single missed reads do not cause flicker.
16. As an operator, I want stale USB feeds to be surfaced after no successful frame arrives for about one second, so that halted feeds are visible without noisy transient warnings.
17. As an operator, I want stale status to be runtime status rather than a persisted camera setting, so that temporary feed health does not pollute Session configuration.
18. As an operator, I want hard USB startup failures to leave the Camera Source in the Session list, so that Camera Zone binding and configuration context are not lost.
19. As an operator, I want USB failures to be shown as per-camera status, so that a source problem does not become an Ambiguity Alert unless downstream Train State becomes unsafe.
20. As an operator, I want failed USB tiles to show a clear failed placeholder or badge, so that runtime failure is more prominent than the intended raw feed mode.
21. As an operator, I want starting or restarting USB tiles to show a placeholder, so that I can distinguish transitional states from live video.
22. As an operator, I want frame age shown only for stale or failed USB sources, so that normal monitoring stays uncluttered.
23. As an operator, I want USB Camera Source startup to be serialized, so that fragile camera backends are not asked to open several devices simultaneously.
24. As an operator, I want each USB Camera Source to read frames independently, so that one slow or failing camera does not delay other cameras.
25. As an operator, I want live USB feeds to prioritize the latest frame over queued backlog, so that monitoring stays low-latency.
26. As an operator, I want UI tile rendering to stay throttled, so that high-FPS cameras do not overload the UI.
27. As an operator, I want a USB tile and selected-camera processing to consume the same physical feed when they refer to the same Camera Source, so that processing does not break live viewing.
28. As an operator, I want calibration and background sampling for USB cameras to use the shared source owner, so that calibration does not open a competing device handle.
29. As an operator, I want USB processing to wait for fresh frame versions where possible, so that it does not repeatedly process the same captured frame.
30. As an operator, I want the owner to keep only the latest frame snapshot, so that Train State processing is based on current observation rather than stale backlog.
31. As an operator, I want USB Camera Source settings to be editable per selected camera, so that each physical camera can use a suitable capture mode.
32. As an operator, I want USB capture settings in the Camera Panel, so that source-specific settings live with the selected Camera Source context.
33. As an operator, I want USB capture settings shown only for USB Camera Sources, so that video file camera configuration is not cluttered with irrelevant controls.
34. As an operator, I want to choose from preset USB resolutions, so that setup is simple and avoids unreliable backend probing.
35. As an operator, I want the first resolution presets to include 640x480, 1280x720, and 1920x1080, so that I can choose low-latency, HD, or high-detail capture.
36. As an operator, I want target FPS choices of 30 and 60, so that I can choose a simple performance target without overcomplicating setup.
37. As an operator, I want the same resolution and FPS preset list available to every USB Camera Source, so that the UI stays consistent while choices remain per source.
38. As an operator, I want requested USB capture settings persisted across app restarts, so that stable camera setup is retained.
39. As an operator, I want requested capture settings stored separately from actual runtime mode, so that backend fallback does not overwrite my intent.
40. As an operator, I want the app to show when the camera accepted a different actual mode than requested, so that I can diagnose fallback behavior.
41. As an operator, I want a USB owner to fail only when the camera cannot return frames, so that unsupported requested modes can fall back without blocking use.
42. As an operator, I want USB capture settings changes batched behind Apply, so that width, height, and FPS edits do not restart the device repeatedly.
43. As an operator, I want Revert to restore the last persisted requested USB settings, so that I can abandon draft changes safely.
44. As an operator, I want Apply to persist requested USB settings immediately, so that my configuration intent survives restart even if runtime falls back.
45. As an operator, I want capture setting changes for visible-only USB cameras to restart that one owner automatically, so that settings take effect without restarting all feeds.
46. As an operator, I want capture setting changes for failed USB owners to require an explicit restart action, so that hardware recovery remains deliberate while I edit settings.
47. As an operator, I want capture setting changes protected while the Vision Pipeline is processing that camera, so that capture restart does not invalidate calibration or Train State continuity.
48. As an operator, I want pending USB capture settings to show requested values in controls and active runtime mode in status, so that intent and current behavior are both clear.
49. As an operator, I want pending USB capture settings to apply after the Vision Pipeline stops if the camera is still visible, so that protected changes take effect at a safe point.
50. As an operator, I want USB capture settings pending for an included running camera to mark pending Vision Pipeline restart, so that processing-relevant source changes are visible globally.
51. As an operator, I want a targeted Restart Camera Source action for USB sources, so that I can recover one failed camera without restarting all feeds.
52. As an operator, I want Restart Camera Source in the selected Camera Panel, so that the action is scoped to the selected Camera Source.
53. As an operator, I want Restart Camera Source disabled while the Vision Pipeline is actively processing that camera, so that I do not disrupt Train State continuity.
54. As an operator, I want restart to preserve previous failure details while starting, so that diagnostics are not lost during recovery.
55. As an operator, I want live physical Camera Sources to have one owning feed even with multiple consumers, so that UI viewing, calibration, and processing do not compete for the USB device.
56. As an operator, I want file-based camera sources to remain unchanged in this iteration, so that the fix stays focused on physical USB device ownership.
57. As an operator, I want this work not to claim simultaneous Train State processing across multiple Camera Zones, so that expectations stay aligned with the current Vision Pipeline scope.
58. As an operator, I want raw visible feeds to keep their RAW FEED meaning, so that shared ownership does not change operator-facing semantics.
59. As an operator, I want no Ambiguity Alert raised solely because a USB Camera Source failed, so that identity intervention remains reserved for unsafe Train State ambiguity.
60. As an operator, I want adding a new camera while the Vision Pipeline is running to avoid silently expanding processing, so that raw visibility does not imply Train State contribution.

## Implementation Decisions

- Add a deep USB Camera Source owner manager that owns live physical USB feeds for the current Session and exposes a small, stable API for acquiring/releasing consumers, getting runtime state, restarting one source, and reading latest-frame snapshots.
- Key USB owners internally by a typed USB camera key containing camera index and capture API; keep source ID strings at UI boundaries.
- Enforce one owner per typed USB key. Requests for an existing key return the existing owner/lease instead of opening a second physical capture.
- Owners have first-iteration states `stopped`, `starting`, `running`, and `failed`.
- Stale feed status is derived from latest frame age while state remains `running`; no successful frame for about one second counts as stale.
- Failed state is driven by open failure or hard read failure, not by timing alone.
- Do not auto-reopen after hard read/open failure in the first iteration.
- Add targeted Restart Camera Source support for USB sources, disabled while that source is actively consumed by selected-camera Vision Pipeline processing.
- Use a two-second grace period before stopping an owner after its final consumer disappears.
- Serialize owner startup globally for safer interaction with OpenCV backends and USB buses.
- Run one frame-reading task per USB owner.
- Frame delivery is pull-based: consumers can get the latest immutable snapshot and processing can wait for a next frame version.
- Latest-frame snapshots include source ID, timestamp, width, height, encoded JPEG bytes, and monotonically increasing frame version.
- Owners encode captured frames to JPEG immediately and keep only the latest snapshot.
- First iteration decodes JPEG snapshots back to processing image data where needed rather than exposing mutable raw frame objects across the ownership boundary.
- Apply requested FPS to capture configuration, then read as fast as the backend returns frames rather than adding a second pacing timer.
- Add a small USB capture backend abstraction so automated tests can simulate device behavior without physical cameras.
- Refactor USB usages in the bug path behind the shared owner model: raw tile preview, USB calibration/background sampling, selected USB processing where touched, and discovery avoidance for already-owned devices.
- Leave video file capture and playback paths unchanged.
- Diff camera tile consumers by desired camera state rather than canceling and rebuilding all tile preview loops on every camera grid refresh.
- Adding a new USB Camera Source starts only that new source/consumer when needed and must not restart existing USB owners.
- Discovery receives already-added USB source identities from the camera list, shows those entries disabled as already added, and avoids probing them.
- Duplicate USB Camera Sources are blocked by source identity in the first iteration; stronger physical-device detection beyond source ID/API aliases is out of scope.
- USB owners start when needed by Camera Visibility or selected-camera processing, not immediately just because a source exists in the Session.
- Owners are Session runtime resources and are stopped/disposed when the app closes or cameras are cleared.
- Per-camera USB runtime status is derived from the owner manager, not persisted camera configuration.
- Failed USB cameras remain in the Session list and retain Camera Zone binding/configuration context.
- Starting/restarting tiles show a placeholder. Failed tiles show a prominent failed state. Normal running tiles keep existing raw feed semantics.
- USB capture settings are separate Camera Source settings, not runtime processing settings.
- USB capture settings are persisted per Camera Source and are editable in the selected Camera Panel.
- USB capture settings UI appears only for USB Camera Sources.
- USB capture settings expose a fixed resolution preset list of 640x480, 1280x720, and 1920x1080, plus target FPS choices 30 and 60.
- Requested USB capture settings are persisted separately from actual reported runtime mode.
- If a requested mode is not accepted, startup can still succeed by reporting actual mode, e.g. requested 1280x720@60, running 640x480@30.
- USB capture setting edits are batched behind explicit Apply and Revert controls.
- Apply persists requested settings immediately.
- Revert restores the last persisted requested settings, not actual runtime fallback values.
- Visible-only USB owners restart automatically when applied capture settings change.
- Failed USB owners do not auto-restart after settings changes; the operator must use Restart Camera Source.
- Capture settings changes for a camera actively processed by the Vision Pipeline are saved as pending until the Vision Pipeline stops; they mark pending Vision Pipeline restart.
- Controls show requested pending values, while per-camera status shows active runtime mode.
- Pending USB capture settings apply automatically after Vision Pipeline stops if the camera is still visible; if hidden and excluded, the next owner start uses the pending settings.
- Preserve ADR 0004 behavior for Vision Pipeline Inclusion in this PRD; do not use this USB fix to redefine inclusion semantics.
- Align with ADR 0005: live physical Camera Sources have one session-owned feed owner shared by UI and selected processing consumers; file-based sources remain unchanged.
- Deep module opportunities:
- `UsbCameraOwnerManager`: session-owned physical source lifecycle, leases, status, restart, and latest snapshot API.
- `UsbCaptureBackend`: small injectable hardware abstraction used by the owner and fake tests.
- `UsbCaptureSettingsService`: requested settings persistence, draft/apply/revert, pending-vs-active status, and mode fallback reporting.
- `CameraTileFeedCoordinator`: computes desired tile consumers from camera visibility/render state and diffs start/stop without disrupting unchanged feeds.
- `UsbCameraDiscoveryService`: lists candidate USB sources while avoiding probes of already-added/owned sources.
- `CameraSourceStatusProjection`: projects owner state, stale status, actual mode, pending settings, and action enablement into selected-panel and tile status.

## Testing Decisions

- Good tests assert external behavior: existing USB feed owners remain running when another source is added, duplicate sources are blocked, stale/failed/starting status is projected correctly, settings apply/revert/pending behavior is correct, and processing consumers do not open competing device handles.
- Tests should not rely on physical USB cameras. Use an injectable fake capture backend that can simulate successful opens, blocked reads, failed opens, hard read failures, unsupported requested modes, fallback actual modes, and multiple source keys.
- Test the USB owner manager as a deep module: one owner per key, lease/reference behavior, two-second grace stop, serialized startup, latest snapshot versioning, stale derivation, failed transitions, and restart action behavior.
- Test the capture settings service: per-source persistence, default values, draft Apply/Revert, requested-vs-actual separation, pending behavior while processing, and no auto-restart for failed owners after settings edits.
- Test the tile feed coordinator: adding one visible USB source starts only that source, unchanged visible USB sources are not stopped/restarted, hidden/excluded sources release after grace, and visible/excluded sources remain raw feed consumers.
- Test discovery behavior: already-added USB sources are listed disabled and are not probed, while unknown candidate indices are probed.
- Test status projection: `starting`, `running`, stale, `failed`, pending settings, actual mode fallback, and Restart Camera Source enablement are rendered as expected.
- Test selected USB processing/calibration integration where touched: processing acquires a shared owner lease and consumes frame versions rather than opening direct duplicate capture handles.
- Prior art exists in desktop service-focused tests for camera grid projection, workspace behavior, Camera Zone identity/binding, settings persistence, and end-to-end reload behavior.
- Prior art exists in Vision and Desktop tests for cancellation-safe processing and runtime behavior; new tests should follow that style by isolating behavior in services rather than asserting private UI event wiring.

## Out of Scope

- Simultaneous Train State processing across multiple Camera Zones.
- Changing or superseding ADR 0004 Vision Pipeline Inclusion semantics.
- Generalizing file-based camera sources into the shared owner abstraction.
- Strong hardware identity detection beyond source ID/API-based duplicate blocking.
- Automatic reconnect/retry policy after hard USB failure.
- Raw frame sharing across consumers without JPEG encoding/decoding.
- Long queue-based frame delivery or backlog processing.
- Pixel-perfect visual design beyond the required Camera Panel controls, placeholders, badges, and status visibility.
- Backend/API switching after a USB source has been added, beyond the existing add-camera discovery behavior.
- Buffer size configuration; keep low-latency buffer behavior fixed for now.

## Further Notes

- This PRD follows the glossary terms `Camera Source`, `Camera Zone`, `Camera Visibility`, `Vision Pipeline Inclusion`, `Vision Pipeline`, and `Session`.
- `Camera Source` was added to the glossary during design resolution: live physical camera sources should have one owning feed even when multiple parts of the system view or process them.
- ADR 0005 records the architectural decision for session-owned USB Camera Source feeds.
- The immediate acceptance focus is that visible USB feeds do not halt when another USB camera is added, while preserving video file playback behavior.
- The implementation should avoid introducing UI claims that a visible raw feed contributes Train State unless Vision Pipeline processing actually includes it.
