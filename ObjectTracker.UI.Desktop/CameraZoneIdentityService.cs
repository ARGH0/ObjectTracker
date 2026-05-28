using System;
using System.Collections.Generic;

namespace ObjectTracker.UI.Desktop;

public sealed class CameraZoneIdentityService
{
    private readonly Dictionary<string, CameraZoneDefinition> zonesById;
    private readonly Dictionary<string, string> zoneIdBySourceId;
    private readonly Func<string> zoneIdFactory;

    public CameraZoneIdentityService(
        IEnumerable<CameraZoneDefinition>? existingZones = null,
        IEnumerable<CameraZoneBinding>? existingBindings = null,
        Func<string>? zoneIdFactory = null)
    {
        zonesById = new Dictionary<string, CameraZoneDefinition>(StringComparer.OrdinalIgnoreCase);
        zoneIdBySourceId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        this.zoneIdFactory = zoneIdFactory ?? (() => Guid.NewGuid().ToString("N"));

        if (existingZones is not null)
        {
            foreach (var zone in existingZones)
            {
                if (string.IsNullOrWhiteSpace(zone.CameraZoneId))
                {
                    continue;
                }

                zonesById[zone.CameraZoneId] = zone with { Name = NormalizeZoneName(zone.Name, zone.CameraZoneId) };
            }
        }

        if (existingBindings is not null)
        {
            foreach (var binding in existingBindings)
            {
                if (string.IsNullOrWhiteSpace(binding.SourceId)
                    || string.IsNullOrWhiteSpace(binding.CameraZoneId)
                    || !zonesById.ContainsKey(binding.CameraZoneId))
                {
                    continue;
                }

                zoneIdBySourceId[binding.SourceId] = binding.CameraZoneId;
            }
        }
    }

    public IReadOnlyCollection<CameraZoneDefinition> CameraZones => zonesById.Values;

    public IReadOnlyList<CameraZoneDefinition> GetCameraZonesOrderedByName()
    {
        var zones = new List<CameraZoneDefinition>(zonesById.Values);
        zones.Sort((left, right) => string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase));
        return zones;
    }

    public IReadOnlyCollection<CameraZoneBinding> SourceBindings
    {
        get
        {
            var bindings = new List<CameraZoneBinding>(zoneIdBySourceId.Count);
            foreach (var (sourceId, cameraZoneId) in zoneIdBySourceId)
            {
                bindings.Add(new CameraZoneBinding(sourceId, cameraZoneId));
            }

            return bindings;
        }
    }

    public CameraZoneBindingResult AssignSourceToZone(string sourceId, string? requestedCameraZoneId = null, string? requestedZoneName = null)
    {
        if (string.IsNullOrWhiteSpace(sourceId))
        {
            throw new ArgumentException("Source id is required.", nameof(sourceId));
        }

        if (!string.IsNullOrWhiteSpace(requestedCameraZoneId))
        {
            if (!zonesById.TryGetValue(requestedCameraZoneId, out var existingZone))
            {
                throw new InvalidOperationException($"Camera Zone '{requestedCameraZoneId}' does not exist.");
            }

            zoneIdBySourceId[sourceId] = existingZone.CameraZoneId;
            return new CameraZoneBindingResult(existingZone, new CameraZoneBinding(sourceId, existingZone.CameraZoneId), CreatedNewZone: false);
        }

        if (zoneIdBySourceId.TryGetValue(sourceId, out var zoneId)
            && zonesById.TryGetValue(zoneId, out var alreadyBoundZone))
        {
            return new CameraZoneBindingResult(alreadyBoundZone, new CameraZoneBinding(sourceId, zoneId), CreatedNewZone: false);
        }

        var newZoneId = zoneIdFactory();
        var zoneName = NormalizeZoneName(requestedZoneName, newZoneId);
        var zone = new CameraZoneDefinition(newZoneId, zoneName);
        zonesById[newZoneId] = zone;
        zoneIdBySourceId[sourceId] = newZoneId;

        return new CameraZoneBindingResult(zone, new CameraZoneBinding(sourceId, newZoneId), CreatedNewZone: true);
    }

    public CameraZoneDefinition CreateCameraZone(string? requestedZoneName = null)
    {
        var newZoneId = zoneIdFactory();
        var zoneName = NormalizeZoneName(requestedZoneName, newZoneId);
        var zone = new CameraZoneDefinition(newZoneId, zoneName);
        zonesById[newZoneId] = zone;
        return zone;
    }

    public bool TryGetCameraZoneForSource(string sourceId, out CameraZoneDefinition zone)
    {
        zone = default;

        if (string.IsNullOrWhiteSpace(sourceId))
        {
            return false;
        }

        if (!zoneIdBySourceId.TryGetValue(sourceId, out var zoneId))
        {
            return false;
        }

        return zonesById.TryGetValue(zoneId, out zone);
    }

    public void RemoveSourceBinding(string sourceId)
    {
        if (string.IsNullOrWhiteSpace(sourceId))
        {
            return;
        }

        zoneIdBySourceId.Remove(sourceId);
    }

    private static string NormalizeZoneName(string? requestedZoneName, string fallbackId)
    {
        if (!string.IsNullOrWhiteSpace(requestedZoneName))
        {
            return requestedZoneName.Trim();
        }

        return $"Camera Zone {fallbackId}";
    }
}

public readonly record struct CameraZoneDefinition(string CameraZoneId, string Name);

public readonly record struct CameraZoneBinding(string SourceId, string CameraZoneId);

public readonly record struct CameraZoneBindingResult(
    CameraZoneDefinition CameraZone,
    CameraZoneBinding Binding,
    bool CreatedNewZone);
