using System;
using System.Collections.Generic;
using System.Linq;

namespace ObjectTracker.UI.Desktop;

public sealed class EffectiveZoneCompositionService
{
    public IReadOnlyList<EffectiveGridCellResult> Compose(
        string cameraZoneId,
        IReadOnlyCollection<CameraZoneLayer> layers,
        LayerTypeCatalogService layerTypeCatalog)
    {
        var rules = layerTypeCatalog.GetOrderedByPrecedence()
            .ToDictionary(definition => definition.LayerTypeId, definition => definition, StringComparer.OrdinalIgnoreCase);

        var candidatesByCell = new Dictionary<GridCell, List<Candidate>>();
        foreach (var layer in layers.Where(layer => string.Equals(layer.CameraZoneId, cameraZoneId, StringComparison.OrdinalIgnoreCase)))
        {
            if (!rules.TryGetValue(layer.LayerTypeId, out var rule))
            {
                continue;
            }

            foreach (var region in layer.Regions)
            {
                foreach (var cell in region.Cells)
                {
                    if (!candidatesByCell.TryGetValue(cell, out var candidates))
                    {
                        candidates = new List<Candidate>();
                        candidatesByCell[cell] = candidates;
                    }

                    candidates.Add(new Candidate(rule, layer.LayerId, region.RegionId, cell));
                }
            }
        }

        var result = new List<EffectiveGridCellResult>();
        foreach (var (_, candidates) in candidatesByCell)
        {
            var winner = candidates
                .OrderBy(candidate => candidate.Rule.Precedence)
                .ThenBy(candidate => candidate.LayerId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(candidate => candidate.RegionId, StringComparer.OrdinalIgnoreCase)
                .First();

            var effectiveRegionId = winner.Rule.MergePolicy == LayerMergePolicy.MergeForEffectiveMask
                ? null
                : winner.RegionId;

            result.Add(new EffectiveGridCellResult(
                winner.Cell,
                winner.Rule.LayerTypeId,
                winner.LayerId,
                effectiveRegionId,
                winner.Rule.Precedence,
                winner.Rule.MergePolicy));
        }

        return result
            .OrderBy(item => item.Cell.Row)
            .ThenBy(item => item.Cell.Column)
            .ToList();
    }

    private readonly record struct Candidate(
        LayerTypeDefinition Rule,
        string LayerId,
        string RegionId,
        GridCell Cell);
}

public readonly record struct EffectiveGridCellResult(
    GridCell Cell,
    string LayerTypeId,
    string LayerId,
    string? RegionId,
    int Precedence,
    LayerMergePolicy MergePolicy)
{
    public string DisplayText =>
        $"[{Cell.Column},{Cell.Row}] -> {LayerTypeId} (p{Precedence})"
        + (RegionId is null ? " [merged]" : $" region={RegionId}");
}
