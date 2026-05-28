# Camera Zone Layer And Region Persistence Model

Type: AFK  
Labels: ready-for-agent

## Parent

- .scratch/issues/0009-camera-zone-grid-layering-foundation-prd.md

## What to build

Add Camera Zone-authored layer and region persistence with stable region identity. Support multiple layers of the same layer type and preserve authored region boundaries. Regions must include immutable `RegionId`, editable name, optional numeric code, and grid-based geometry.

## Acceptance criteria

- [ ] Multiple layers of the same layer type can be saved and restored per Camera Zone.
- [ ] Region identity (`RegionId`) is immutable while name/code remain editable.
- [ ] Camera Zone layer and region data persists and reloads correctly across Sessions.

## Blocked by

- .scratch/issues/camera-zone-grid-layering-foundation-prd/0001-camera-zone-identity-and-source-rebind-foundation.md
- .scratch/issues/camera-zone-grid-layering-foundation-prd/0003-layer-type-definition-catalog-with-precedence-and-merge-policy.md
