# Single Loopable Video Camera Source

Type: AFK  
Labels: ready-for-agent

## Parent

.scratch/issues/0030-vision-pipeline-snapshot-frame-feed-prd.md

## What to build

Replace file-based Camera Source playlist semantics with one loopable video source end-to-end. Operators should configure one video path and per-source loop behavior, and raw tile playback should follow that single-source model.

## Acceptance criteria

- [ ] A file-based Camera Source has one video path and one per-source loop setting.
- [ ] Playlist progression no longer drives operator-facing runtime behavior.
- [ ] Existing raw tile playback works for loop and no-loop file-based Camera Sources.
- [ ] Tests cover persistence/projection and runtime playback behavior for the single loopable video source model.

## Blocked by

None - can start immediately
