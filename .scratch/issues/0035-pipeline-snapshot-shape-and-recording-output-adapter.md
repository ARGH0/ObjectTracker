# PipelineSnapshot Shape And Recording Output Adapter

Type: AFK  
Labels: ready-for-agent

## Parent

.scratch/issues/0030-vision-pipeline-snapshot-frame-feed-prd.md

## What to build

Deepen the PipelineSnapshot output seam so snapshots are per Camera Source and carry Source Frame, Annotated Frame, observations, relevant Train States, and frame timing metadata. Add a recording output adapter for tests.

## Acceptance criteria

- [ ] PipelineSnapshot is per Camera Source and routable by Camera Source ID.
- [ ] PipelineSnapshot carries Source Frame and Annotated Frame in the Vision Pipeline coordinate space.
- [ ] PipelineSnapshot can carry Moving Object Observations, Train Observations, relevant Train States, and timing metadata.
- [ ] PipelineSnapshot does not carry Camera Source Status, Vision Pipeline Lane Status, global Vision Pipeline status, failure messages, restart eligibility, or Ambiguity Alert state.
- [ ] Tests can record emitted snapshots through an output adapter.

## Blocked by

None - can start immediately
