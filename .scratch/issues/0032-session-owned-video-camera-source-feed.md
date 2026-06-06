# Session-Owned Video Camera Source Feed

Type: AFK  
Labels: ready-for-agent

## Parent

.scratch/issues/0030-vision-pipeline-snapshot-frame-feed-prd.md

## What to build

Add a session-owned feed for file-based Camera Sources so raw display and later Vision Pipeline lanes consume the same playback timeline instead of opening independent video playback paths.

## Acceptance criteria

- [ ] A file-based Camera Source has one session-owned playback feed during a Session.
- [ ] Multiple consumers observe the same playback timeline without independently advancing the video.
- [ ] Camera Source Status can be reported for file-based playback.
- [ ] Tests use fake file feed adapters and do not depend on real-time playback.

## Blocked by

- .scratch/issues/0031-single-loopable-video-camera-source.md
