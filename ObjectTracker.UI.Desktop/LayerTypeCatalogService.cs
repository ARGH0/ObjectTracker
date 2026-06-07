using System;
using System.Collections.Generic;
using System.Linq;
using ObjectTracker.UI.Desktop.Enums;

namespace ObjectTracker.UI.Desktop;

public sealed class LayerTypeCatalogService
{
    private readonly Dictionary<string, LayerTypeDefinition> definitionsById;

    private LayerTypeCatalogService(Dictionary<string, LayerTypeDefinition> definitionsById)
    {
        this.definitionsById = definitionsById;
    }

    public static LayerTypeCatalogService CreateDefault()
    {
        return Create(new[]
        {
            new LayerTypeDefinition("no-vision", "No-Vision", 0, LayerMergePolicy.PreserveRegions, LayerTypeBehaviorClass.LogicCoupled),
            new LayerTypeDefinition("high-caution", "High-Caution", 10, LayerMergePolicy.PreserveRegions, LayerTypeBehaviorClass.LogicCoupled),
            new LayerTypeDefinition("rail-roi", "Rail ROI", 20, LayerMergePolicy.PreserveRegions, LayerTypeBehaviorClass.LogicCoupled),
            new LayerTypeDefinition("neutral", "Neutral", 30, LayerMergePolicy.MergeForEffectiveMask, LayerTypeBehaviorClass.Informational)
        });
    }

    public static LayerTypeCatalogService Create(IEnumerable<LayerTypeDefinition> definitions)
    {
        var byId = new Dictionary<string, LayerTypeDefinition>(StringComparer.OrdinalIgnoreCase);
        var seenPrecedence = new HashSet<int>();

        foreach (var definition in definitions)
        {
            if (string.IsNullOrWhiteSpace(definition.LayerTypeId))
            {
                throw new InvalidOperationException("Layer type id is required.");
            }

            if (string.IsNullOrWhiteSpace(definition.DisplayName))
            {
                throw new InvalidOperationException($"Layer type '{definition.LayerTypeId}' requires a display name.");
            }

            if (!seenPrecedence.Add(definition.Precedence))
            {
                throw new InvalidOperationException($"Duplicate precedence '{definition.Precedence}' is not allowed.");
            }

            var normalizedId = definition.LayerTypeId.Trim().ToUpperInvariant();
            var normalizedName = definition.DisplayName.Trim();
            byId[normalizedId] = definition with { LayerTypeId = normalizedId, DisplayName = normalizedName };
        }

        if (byId.Count == 0)
        {
            throw new InvalidOperationException("At least one layer type definition is required.");
        }

        return new LayerTypeCatalogService(byId);
    }

    public IReadOnlyList<LayerTypeDefinition> GetOrderedByPrecedence()
    {
        return definitionsById.Values
            .OrderBy(definition => definition.Precedence)
            .ThenBy(definition => definition.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public LayerTypeCatalogService AddLayerType(LayerTypeDefinition definition)
    {
        var definitions = GetOrderedByPrecedence().ToList();
        definitions.Add(definition);
        return Create(definitions);
    }

    public LayerTypeAddResult TryAddLayerType(LayerTypeDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(definition.LayerTypeId))
        {
            return new LayerTypeAddResult(false, this, "Layer Type ID is required.");
        }

        if (string.IsNullOrWhiteSpace(definition.DisplayName))
        {
            return new LayerTypeAddResult(false, this, $"Layer Type '{definition.LayerTypeId}' requires a display name.");
        }

        var normalizedId = definition.LayerTypeId.Trim().ToUpperInvariant();
        if (definitionsById.ContainsKey(normalizedId))
        {
            return new LayerTypeAddResult(false, this, $"Layer Type '{normalizedId}' already exists.");
        }

        if (definitionsById.Values.Any(existing => existing.Precedence == definition.Precedence))
        {
            return new LayerTypeAddResult(false, this, $"Duplicate precedence '{definition.Precedence}' is not allowed.");
        }

        return new LayerTypeAddResult(true, AddLayerType(definition), string.Empty);
    }

    public LayerTypeCatalogService RemoveLayerType(string layerTypeId)
    {
        var definitions = GetOrderedByPrecedence()
            .Where(definition => !string.Equals(definition.LayerTypeId, layerTypeId, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return Create(definitions);
    }
}

public readonly record struct LayerTypeDefinition(
    string LayerTypeId,
    string DisplayName,
    int Precedence,
    LayerMergePolicy MergePolicy,
    LayerTypeBehaviorClass BehaviorClass);

public readonly record struct LayerTypeAddResult(bool Added, LayerTypeCatalogService Catalog, string Warning);
