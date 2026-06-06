# Vision Pipeline Snapshot Frame Feed PRD

Type: AFK  
Labels: ready-for-agent

## What to build

Adopt the PRD for making PipelineSnapshot the UI frame feed for included Camera Sources, while separating Camera Source Status, Vision Pipeline Lane Status, and Train Tracking from frame output.

PRD: `docs/prd/vision-pipeline-snapshot-frame-feed-prd.md`

The slice is complete when implementation planning and execution track against the PRD decisions for:

- per-Camera Source PipelineSnapshot output for Source Frame, Annotated Frame, optional Debug Frames, observations, and relevant Train States
- visible/included UI tiles using PipelineSnapshot while the Vision Pipeline is running
- visible/excluded and stopped visible Camera Sources using raw Camera Source feed outside the Vision Pipeline
- typed Camera Source Status and Vision Pipeline Lane Status outside PipelineSnapshot
- one Vision Pipeline runtime with multiple Camera Source lanes and internal Train Tracking
- single loopable file-based Camera Sources instead of video playlists
- session-owned Camera Source feeds for USB and file-based sources

## Acceptance criteria

- [ ] PRD is complete and reflects resolved design decisions from the architecture grilling session.
- [ ] PRD uses the domain vocabulary from `CONTEXT.md`.
- [ ] PRD respects ADR-0004, ADR-0005, and ADR-0006.
- [ ] Issue is labeled `ready-for-agent`.
- [ ] The implementation can be broken into independently grabbable issues from this PRD.

## Notes

- This issue tracks PRD publication/readiness only, not implementation.
- GitHub publishing was not available in this environment because the `gh` CLI is not installed; this repo already uses `.scratch/issues` as a local issue tracker.
