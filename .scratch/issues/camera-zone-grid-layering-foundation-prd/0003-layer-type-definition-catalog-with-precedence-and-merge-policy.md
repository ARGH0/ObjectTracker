# Layer Type Definition Catalog With Precedence And Merge Policy

Type: AFK  
Labels: ready-for-agent

## Parent

- .scratch/issues/0009-camera-zone-grid-layering-foundation-prd.md

## What to build

Create an app-wide layer type catalog where each layer type defines numeric precedence (`0` highest) and merge policy (`preserve_regions` or `merge_for_effective_mask`). Use this catalog as the single source of truth across all Camera Zones.

## Acceptance criteria

- [ ] Layer types are configurable application-wide with stable identity and display name.
- [ ] Numeric precedence is validated and used for deterministic ordering.
- [ ] Merge policy is stored per layer type and available to Camera Zone composition behavior.

## Blocked by

- .scratch/issues/camera-zone-grid-layering-foundation-prd/0002-app-wide-grid-settings-with-default-32x18.md
