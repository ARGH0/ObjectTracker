# Close-First Motion Mask Refinement

Type: AFK  
Labels: ready-for-agent

## What to build

Update motion-mask refinement to use a close-first morphology flow so fragmented Train motion pixels are connected before optional cleanup, and expose the result in existing detection previews. The slice is complete when moving Train blobs become materially more connected in low-contrast rail scenes.

## Acceptance criteria

- [ ] Morphology flow is threshold -> close-first refinement -> optional light cleanup before contour extraction.
- [ ] Low-contrast Train motion in preview is more connected than baseline for representative frames.
- [ ] Runtime tuning remains compatible with the updated refinement flow.

## Blocked by

- .scratch/issues/0001-rail-roi-gate-in-motion-detection-preview.md
