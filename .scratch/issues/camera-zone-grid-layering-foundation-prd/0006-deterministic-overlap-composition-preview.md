# Deterministic Overlap Composition Preview

Type: AFK  
Labels: ready-for-agent

## Parent

- .scratch/issues/0009-camera-zone-grid-layering-foundation-prd.md

## What to build

Add a composition preview that shows the effective result of overlapping regions according to app-wide layer type precedence and merge policy, while preserving authored layer and region structure.

## Acceptance criteria

- [ ] Overlap outcomes follow numeric precedence rules (`0` highest).
- [ ] Merge behavior follows each layer type definition.
- [ ] Preview is deterministic and updates from live editor changes for the selected Camera Zone.

## Blocked by

- .scratch/issues/camera-zone-grid-layering-foundation-prd/0003-layer-type-definition-catalog-with-precedence-and-merge-policy.md
- .scratch/issues/camera-zone-grid-layering-foundation-prd/0004-camera-zone-layer-and-region-persistence-model.md
- .scratch/issues/camera-zone-grid-layering-foundation-prd/0005-grid-overlay-editor-in-camera-zone-configuration-view.md
