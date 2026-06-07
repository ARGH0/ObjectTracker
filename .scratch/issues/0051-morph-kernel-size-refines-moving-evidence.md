# Morph Kernel Size Refines Moving Evidence

Type: AFK  
Labels: done

## Parent

docs/prd/vision-pipeline-visual-observation-integration-prd.md

## What to build

Use motion mask refinement in the visual observation module so Morph Kernel Size affects fragmented moving evidence. The completed slice should preserve bounded cleanup behavior while making moving Train evidence less fragmented.

## Acceptance criteria

- [x] The visual observation module refines foreground masks before extracting Moving Object Observations.
- [x] Morph Kernel Size affects the refinement behavior through observation settings.
- [x] Tests prove fragmented motion can be connected by refinement.
- [x] Tests prove refinement does not turn tiny noise below Motion Area into valid moving evidence.

## Blocked by

- .scratch/issues/0049-foreground-motion-produces-moving-object-observations.md
