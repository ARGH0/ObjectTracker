# Issue 13: UI workspace integration and layer cleanup [HITL]

**PRD Reference:** [Region System PRD](../../prd/region-system-prd.md)

## What to build

Replace the old layer system UI with the new Region workspace. This is a HITL (Human-in-the-Loop) slice — work step by step with an agent, checking UI results as we go.

**Cleanup (delete):**
- Remove all layer-related UI from `MainWindow.axaml.cs`: layer dropdown, layer list box, draw-on-grid button, refresh-composition-preview button
- Remove layer workspace tab/panel
- Remove `LayerTypeCatalogService`, `CameraZoneLayerEditorService`, `CameraZoneLayerRepository`, `EffectiveZoneCompositionService`, `LayerTypeSettingsStore`, `MotionMaskRefiner`

**Add (new):**
- Region workspace panel with:
  - Region list per selected Camera Zone (name, type, cell count)
  - Create Region button (opens Grid Editor Dialog)
  - Edit Region button (reopens Grid Editor Dialog with existing cells)
  - Delete Region button (with confirmation)
  - Region type selector dropdown when creating
  - Region name input field when creating
- Wire `IRegionManagerService` to the workspace UI
- Register Region workspace in MainWindow navigation

**Integration:**
- Connect Grid Editor Dialog (#4) to Region Manager Service for save/load
- Connect Region Registry (#3) to workspace for live region list updates
- Ensure all previously implemented region behaviors (#8-12) work through the new UI

## Acceptance criteria

- [ ] Old layer UI completely removed from MainWindow (no layer dropdown, no layer list, no draw-on-grid button)
- [ ] Old layer services deleted and no compilation references remain
- [ ] Region workspace panel added with region list, create/edit/delete buttons
- [ ] Region type selector shows all 5 region types
- [ ] Create Region opens Grid Editor Dialog, saves selected cells to new Region
- [ ] Edit Region opens Grid Editor Dialog with existing cells pre-selected
- [ ] Delete Region removes from Registry and persists to `regions.json`
- [ ] Region list updates live when regions are created/edited/deleted (via Registry observer)
- [ ] All 5 region behaviors (EXCLUDE, HIGH_PROBABILITY, ENTER_CROSSROAD, EXIT_CROSSROAD, CAMERA_OVERLAP) work through new UI
- [ ] Session audit log records region actions (create/edit/delete)
- [ ] No regression in existing Vision Pipeline functionality
- [ ] Operator can complete full workflow: create region → edit cells → save → verify behavior in running pipeline

## Blocked by

- #3 (Registry), #4 (Grid editor), #8 (EXCLUDE_REGION)
