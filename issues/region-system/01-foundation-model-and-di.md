# Issue 1: Foundation — Model types, DI registration, and shared contracts

**PRD Reference:** [Region System PRD](../../prd/region-system-prd.md)

## What to build

Establish the foundation for the Region system by creating:

- Pure immutable model types: `Region`, `RegionType` (enum with 5 values), `GridCell`, `CameraZoneId`
- Interface contracts for all services: `IRegionRegistry`, `IRegionPersistence`, `IRegionPriorityResolver`, `ICoordinateMapper`, `IRegionEvaluator`, `IHandoffResolver`, `IRegionProcessorService`, `IRegionManagerService`
- DI container registration via `AddRegionServices()` extension method on `IServiceCollection`
- Lifetime registration: 9 singletons, 1 transient (Grid Editor Dialog)

All types use C# records/structs with no setters. Interfaces are in `Region.Contracts` namespace. Implementations go in `Region.Implementation`.

## Acceptance criteria

- [ ] All model types compile and are immutable (no public setters on domain types)
- [ ] All 8 service interfaces defined with clear method signatures
- [ ] `AddRegionServices()` registers all 10 services with correct lifetimes
- [ ] Startup integration test resolves every service from the container without errors
- [ ] No concrete type is referenced outside its own assembly — all consumers depend only on interfaces
- [ ] Unit tests verify DI wiring correctness (ServiceCollection → resolve → assert not null)

## Blocked by

- None - can start immediately
