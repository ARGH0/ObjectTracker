# Bounded Hole-Fill And Component Merge Policy

Type: AFK  
Labels: ready-for-agent

## What to build

Add bounded hole-fill and bounded component-merge policy to motion-mask refinement so one Train tends to appear as one connected component inside Rail ROI, while preventing accidental merges across nearby Trains or unrelated motion. The slice is complete when fragmented components merge only under explicit safe bounds.

## Acceptance criteria

- [ ] Small interior holes in Train motion blobs are filled within defined bounds.
- [ ] Adjacent components merge only when within configured distance/corridor bounds.
- [ ] Unsafe merges are rejected and reflected correctly in preview output.

## Blocked by

- .scratch/issues/0003-close-first-motion-mask-refinement.md
