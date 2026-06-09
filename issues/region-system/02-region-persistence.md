# Issue 2: Region persistence — load/save `regions.json` with in-memory fake for tests

**PRD Reference:** [Region System PRD](../../prd/region-system-prd.md)

## What to build

Implement `IRegionPersistence` / `RegionPersistence` for JSON serialization/deserialization of all regions to/from `%APPDATA%\ObjectTracker\regions.json`. The file structure matches the PRD schema:

```json
{
  "regions": [
    {
      "id": "guid-string",
      "name": "North Crossing Entry",
      "type": "ENTER_CROSSROAD_REGION",
      "cameraZoneId": "zone-abc-123",
      "cells": [{ "column": 12, "row": 5 }, { "column": 13, "row": 5 }],
      "createdAt": "2026-06-09T10:00:00Z",
      "updatedAt": "2026-06-09T14:30:00Z"
    }
  ]
}
```

CAMERA_OVERLAP_REGION entries include `overlappingZoneIds` array. Load at startup, save on mutation. Provide `FakeRegionPersistence` for tests (in-memory list, no real file I/O).

## Acceptance criteria

- [ ] `LoadAsync()` reads and deserializes `regions.json` into a list of `Region` objects
- [ ] `SaveAsync(list)` writes the region list to `regions.json` with correct schema
- [ ] Load/save round-trip preserves all fields (id, name, type, zoneId, cells, timestamps, overlappingZoneIds)
- [ ] Empty region list serializes to `{ "regions": [] }`
- [ ] `FakeRegionPersistence` implements `IRegionPersistence` with in-memory storage, no file I/O
- [ ] Unit tests verify round-trip for: empty list, single region, multiple zones, CAMERA_OVERLAP_REGION spanning zones

## Blocked by

- #1 (Foundation)
