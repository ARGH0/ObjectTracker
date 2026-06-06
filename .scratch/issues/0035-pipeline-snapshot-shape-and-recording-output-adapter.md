# PipelineSnapshot Shape And Recording Output Adapter

Type: AFK  
Labels: done

## Parent

.scratch/issues/0030-vision-pipeline-snapshot-frame-feed-prd.md

## What to build

Deepen the PipelineSnapshot output seam so snapshots are per Camera Source and carry Source Frame, Annotated Frame, observations, relevant Train States, and frame timing metadata. Add a recording output adapter for tests.

## Acceptance criteria

- [x] PipelineSnapshot is per Camera Source and routable by Camera Source ID.
- [x] PipelineSnapshot carries Source Frame and Annotated Frame in the Vision Pipeline coordinate space.
- [x] PipelineSnapshot can carry Moving Object Observations, Train Observations, relevant Train States, and timing metadata.
- [x] PipelineSnapshot does not carry Camera Source Status, Vision Pipeline Lane Status, global Vision Pipeline status, failure messages, restart eligibility, or Ambiguity Alert state.
- [x] Tests can record emitted snapshots through an output adapter.

## Blocked by

None - can start immediately
