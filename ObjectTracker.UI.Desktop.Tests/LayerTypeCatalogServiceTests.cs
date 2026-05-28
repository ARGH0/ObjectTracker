using System;
using System.Linq;
using Xunit;

namespace ObjectTracker.UI.Desktop.Tests;

public sealed class LayerTypeCatalogServiceTests
{
    [Fact]
    public void CreateDefault_ReturnsDeterministicPrecedenceOrdering()
    {
        var catalog = LayerTypeCatalogService.CreateDefault();

        var definitions = catalog.GetOrderedByPrecedence().ToList();

        Assert.NotEmpty(definitions);
        Assert.True(definitions.Zip(definitions.Skip(1), (left, right) => left.Precedence <= right.Precedence).All(value => value));
    }

    [Fact]
    public void Create_WithDuplicatePrecedence_Throws()
    {
        var duplicate = new[]
        {
            new LayerTypeDefinition("no-vision", "No-Vision", 0, LayerMergePolicy.PreserveRegions, LayerTypeBehaviorClass.LogicCoupled),
            new LayerTypeDefinition("rail-roi", "Rail ROI", 0, LayerMergePolicy.PreserveRegions, LayerTypeBehaviorClass.LogicCoupled)
        };

        var exception = Assert.Throws<InvalidOperationException>(() => LayerTypeCatalogService.Create(duplicate));

        Assert.Contains("Duplicate precedence", exception.Message, StringComparison.Ordinal);
    }
}
