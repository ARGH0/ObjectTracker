using System;
using System.IO;
using System.Linq;
using ObjectTracker.UI.Desktop.Enums;
using Xunit;

namespace ObjectTracker.UI.Desktop.Tests;

public sealed class LayerTypeSettingsStoreTests
{
    /// <summary>
    /// <description>Feature: LayerTypeSettingsStore persists and reloads layer type definitions correctly.
    /// 
    ///   Scenario: Saving a catalog with NO-VISION (precedence 0, PreserveRegions) and NEUTRAL (precedence 10, MergeForEffectiveMask) and reloading should preserve all properties.
    ///     Given a LayerTypeSettingsStore initialized with a temporary file path,
    ///      And Save is called with the ordered precedence catalog containing NO-VISION and NEUTRAL definitions,
    ///     When Load() is called,
    ///     Then loaded definitions count should be 2,
    ///      And definitions[0].LayerTypeId should be "NO-VISION" with Precedence 0,
    ///      And definitions[1].MergePolicy should be MergeForEffectiveMask.</description>
    /// </summary>
    [Fact]
    public void SaveThenLoad_PreservesLayerTypeDefinitions()
    {
        var path = BuildTempFilePath();
        var store = new LayerTypeSettingsStore(path);
        var input = LayerTypeCatalogService.Create(new[]
        {
            new LayerTypeDefinition("no-vision", "No-Vision", 0, LayerMergePolicy.PreserveRegions, LayerTypeBehaviorClass.LogicCoupled),
            new LayerTypeDefinition("neutral", "Neutral", 10, LayerMergePolicy.MergeForEffectiveMask, LayerTypeBehaviorClass.Informational)
        });

        store.Save(input.GetOrderedByPrecedence());

        var loaded = store.Load();
        var definitions = loaded.GetOrderedByPrecedence().ToList();

        Assert.Equal(2, definitions.Count);
        Assert.Equal("NO-VISION", definitions[0].LayerTypeId);
        Assert.Equal(0, definitions[0].Precedence);
        Assert.Equal(LayerMergePolicy.MergeForEffectiveMask, definitions[1].MergePolicy);
    }

    /// <summary>
    /// <description>Feature: LayerTypeSettingsStore returns a default catalog when the file is missing.
    /// 
    ///   Scenario: Loading from a non-existent path produces a catalog with at least one definition.
    ///     Given a LayerTypeSettingsStore initialized with a temporary file path that does not exist,
    ///     When Load() is called,
    ///     Then the loaded catalog should contain at least one definition.</description>
    /// </summary>
    [Fact]
    public void Load_WhenFileMissing_ReturnsDefaultCatalog()
    {
        var store = new LayerTypeSettingsStore(BuildTempFilePath());

        var loaded = store.Load();

        Assert.NotEmpty(loaded.GetOrderedByPrecedence());
    }

    private static string BuildTempFilePath()
    {
        var folder = Path.Combine(Path.GetTempPath(), "ObjectTracker.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, "layer-types.json");
    }
}
