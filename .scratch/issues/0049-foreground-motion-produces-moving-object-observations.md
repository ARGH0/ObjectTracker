# Foreground Motion Produces Moving Object Observations

Type: AFK  
Labels: ready-for-agent

## Parent

docs/prd/vision-pipeline-visual-observation-integration-prd.md

## What to build

Move background-diff foreground extraction and Motion Area filtering into the visual observation module. A fresh Source Frame with foreground motion above the configured threshold and area should emit a Moving Object Observation that can flow through PipelineSnapshot.

## Acceptance criteria

- [ ] Background-diff foreground extraction is available through the visual observation module.
- [ ] Motion Area filters tiny foreground components before they become Moving Object Observations.
- [ ] A test proves foreground motion above the threshold and Motion Area emits a Moving Object Observation.
- [ ] A test proves foreground noise below Motion Area does not emit a Moving Object Observation.

## Blocked by

- .scratch/issues/0047-visual-observation-module-contract.md
