# Issue 4: Grid editor dialog — standalone window for cell selection with MVVM separation [HITL]

**PRD Reference:** [Region System PRD](../../prd/region-system-prd.md)

## What to build

Standalone Avalonia dialog window for visual grid editing. Clean MVVM architecture — no code-behind logic. Features:

- Renders camera frame image (from video file or USB grab) as background
- Overlays fixed rectangular cell grid with clickable `Rectangle` cells
- Single-click toggles cell selection state (red fill when selected)
- Displays region color coding: each cell shows which Region type it belongs to (if any)
- Shows selected cell count and effective Region type in title/status bar
- Save button returns selected cell set; Cancel button discards changes
- Grid auto-scales to fit image area with proper offset

Receives camera frame + grid settings via constructor or dialog parameters. Transient instance per open.

## Acceptance criteria

- [ ] Dialog opens as separate window, decoupled from MainWindow
- [ ] Camera frame renders as background image in dialog
- [ ] Grid cells render as clickable rectangles with proper scaling/offset
- [ ] Single-click toggles cell selection (selected = red fill)
- [ ] Cell color coding shows effective Region type for each cell (if assigned to a region)
- [ ] Selected cell count displayed in UI
- [ ] Save button returns `List<GridCell>` of selected cells
- [ ] Cancel button closes dialog without changes
- [ ] Grid scales correctly when image size changes (different camera sources, resolutions)
- [ ] MVVM architecture: no logic in code-behind, all state in ViewModel
- [ ] Unit tests for ViewModel: click toggles selection, save returns correct cells, cancel discards

## Blocked by

- #1 (Foundation)
