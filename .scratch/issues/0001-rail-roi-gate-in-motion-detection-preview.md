# Rail ROI Gate In Motion Detection Preview

Type: AFK  
Labels: ready-for-agent

## What to build

Implement an end-to-end detection path that applies Rail ROI gating before moving-object extraction for each Camera Zone, and surfaces the gated result in the detection preview. The slice is complete when operators can see motion extraction constrained to Rail ROI instead of full-frame changes.

## Acceptance criteria

- [ ] Motion extraction is constrained by Rail ROI before moving object contour extraction.
- [ ] Detection preview clearly shows Rail ROI-gated motion behavior per Camera Zone.
- [ ] Session audit output captures that Rail ROI-gated detection path was used.

## Blocked by

None - can start immediately.
