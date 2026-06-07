# End-To-End Visual Observation Regression Suite

Type: AFK  
Labels: ready-for-agent

## Parent

docs/prd/vision-pipeline-visual-observation-integration-prd.md

## What to build

Add integration-style regression coverage for the full operator flow: fresh Source Frame input, motion-gated observations, Train State snapshots, Debug View frames, no stale-frame evidence, and no full-frame color blob explosion.

## Acceptance criteria

- [ ] End-to-end coverage proves file and USB Camera Sources use the visual observation path during Vision Pipeline runtime.
- [ ] Coverage proves no PipelineSnapshot is published from stale repeated Source Frames.
- [ ] Coverage proves Debug View receives visual evidence Debug Frames from the same PipelineSnapshot flow as Annotated Frames.
- [ ] Coverage proves full-frame static color blobs do not create Train Observations or Train State.

## Blocked by

- .scratch/issues/0059-default-runtime-uses-visual-observation-path.md
