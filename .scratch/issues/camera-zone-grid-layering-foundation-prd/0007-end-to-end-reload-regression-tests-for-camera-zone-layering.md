# End-To-End Reload Regression Tests For Camera Zone Layering

Type: AFK  
Labels: ready-for-agent

## Parent

- .scratch/issues/0009-camera-zone-grid-layering-foundation-prd.md

## What to build

Add regression coverage that verifies end-to-end Camera Zone layering behavior through public interfaces: app-wide grid settings, Camera Zone binding, authored layers, and regions all persist and reload correctly with deterministic composition outcomes.

## Acceptance criteria

- [ ] Tests verify save/reload behavior for app-wide grid and Camera Zone-authored layers and regions.
- [ ] Tests verify stable `CameraZoneId` and `RegionId` across reload.
- [ ] Tests verify deterministic composition behavior after reload using layer type precedence and merge policy.

## Blocked by

- .scratch/issues/camera-zone-grid-layering-foundation-prd/0001-camera-zone-identity-and-source-rebind-foundation.md
- .scratch/issues/camera-zone-grid-layering-foundation-prd/0002-app-wide-grid-settings-with-default-32x18.md
- .scratch/issues/camera-zone-grid-layering-foundation-prd/0003-layer-type-definition-catalog-with-precedence-and-merge-policy.md
- .scratch/issues/camera-zone-grid-layering-foundation-prd/0004-camera-zone-layer-and-region-persistence-model.md
- .scratch/issues/camera-zone-grid-layering-foundation-prd/0005-grid-overlay-editor-in-camera-zone-configuration-view.md
- .scratch/issues/camera-zone-grid-layering-foundation-prd/0006-deterministic-overlap-composition-preview.md
