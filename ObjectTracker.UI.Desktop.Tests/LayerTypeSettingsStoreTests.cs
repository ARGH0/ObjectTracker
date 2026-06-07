using System;
using System.IO;
using System.Linq;
using Xunit;

namespace ObjectTracker.UI.Desktop.Tests;

public sealed class LayerTypeSettingsStoreTests
{
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
