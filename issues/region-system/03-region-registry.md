# Issue 3: Region registry — in-memory CRUD with thread-safe reads and zone-scoped queries

**PRD Reference:** [Region System PRD](../../prd/region-system-prd.md)

## What to build

Implement `IRegionRegistry` / `RegionRegistry` as the in-memory store for all regions. Features:

- CRUD operations: `Create(Region)`, `Update(Region)`, `Delete(Guid)`, `GetById(Guid)`, `GetAll()`, `GetByZone(CameraZoneId)`
- Thread-safe concurrent reads during Vision Pipeline execution via immutable snapshots
- Initialize from `IRegionPersistence.LoadAsync()` at construction
- Notify observers on mutation for UI bindings

Regions are stored in a single flat collection but logically grouped by `CameraZoneId`. CAMERA_OVERLAP_REGION entries reference multiple zone IDs.

## Acceptance criteria

- [ ] `Create(region)` adds region and returns it with assigned UUID
- [ ] `Update(region)` replaces existing region, preserves ID
- [ ] `Delete(id)` removes region, returns true if found
- [ ] `GetByZone(zoneId)` returns only regions for that zone (including CAMERA_OVERLAP_REGION entries that reference the zone)
- [ ] Immutable snapshot on mutation — concurrent readers never see partially-updated state
- [ ] UI notification on create/update/delete (observer pattern or event args)
- [ ] Unit tests verify CRUD correctness, thread-safety under concurrent read/write, and zone-scoped query accuracy

## Blocked by

- #2 (Persistence)
