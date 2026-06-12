using Microsoft.Extensions.DependencyInjection;
using ObjectTracker.UI.Desktop.Region.Contracts;
using ObjectTracker.UI.Desktop.Region.Model;
using Xunit;

namespace ObjectTracker.UI.Desktop.Tests;

public sealed class RegionDiTests
{
    [Fact]
    public void AddRegionServices_RegistersAllServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IRegionPersistence>(StubPersistence());
        services.AddRegionServices();

        var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<ICoordinateMapper>());
        Assert.NotNull(provider.GetRequiredService<IRegionPersistence>());
        Assert.NotNull(provider.GetRequiredService<IRegionRegistry>());
        Assert.NotNull(provider.GetRequiredService<IHandoffResolver>());
        Assert.NotNull(provider.GetRequiredService<IRegionEvaluator>());
        Assert.NotNull(provider.GetRequiredService<IRegionProcessorService>());
        Assert.NotNull(provider.GetRequiredService<IRegionManagerService>());
    }

    [Fact]
    public void CoordinateMapper_IsSingleton()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IRegionPersistence>(StubPersistence());
        services.AddRegionServices();

        var provider = services.BuildServiceProvider();

        var first = provider.GetRequiredService<ICoordinateMapper>();
        var second = provider.GetRequiredService<ICoordinateMapper>();

        Assert.Same(first, second);
    }

    [Fact]
    public void RegionPersistence_IsSingleton()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IRegionPersistence>(StubPersistence());
        services.AddRegionServices();

        var provider = services.BuildServiceProvider();

        var first = provider.GetRequiredService<IRegionPersistence>();
        var second = provider.GetRequiredService<IRegionPersistence>();

        Assert.Same(first, second);
    }

    [Fact]
    public void RegionRegistry_IsSingleton()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IRegionPersistence>(StubPersistence());
        services.AddRegionServices();

        var provider = services.BuildServiceProvider();

        var first = provider.GetRequiredService<IRegionRegistry>();
        var second = provider.GetRequiredService<IRegionRegistry>();

        Assert.Same(first, second);
    }

    [Fact]
    public void HandoffResolver_IsSingleton()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IRegionPersistence>(StubPersistence());
        services.AddRegionServices();

        var provider = services.BuildServiceProvider();

        var first = provider.GetRequiredService<IHandoffResolver>();
        var second = provider.GetRequiredService<IHandoffResolver>();

        Assert.Same(first, second);
    }

    [Fact]
    public void RegionEvaluator_IsSingleton()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IRegionPersistence>(StubPersistence());
        services.AddRegionServices();

        var provider = services.BuildServiceProvider();

        var first = provider.GetRequiredService<IRegionEvaluator>();
        var second = provider.GetRequiredService<IRegionEvaluator>();

        Assert.Same(first, second);
    }

    [Fact]
    public void RegionProcessorService_IsSingleton()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IRegionPersistence>(StubPersistence());
        services.AddRegionServices();

        var provider = services.BuildServiceProvider();

        var first = provider.GetRequiredService<IRegionProcessorService>();
        var second = provider.GetRequiredService<IRegionProcessorService>();

        Assert.Same(first, second);
    }

    [Fact]
    public void RegionManagerService_IsSingleton()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IRegionPersistence>(StubPersistence());
        services.AddRegionServices();

        var provider = services.BuildServiceProvider();

        var first = provider.GetRequiredService<IRegionManagerService>();
        var second = provider.GetRequiredService<IRegionManagerService>();

        Assert.Same(first, second);
    }

    private static IRegionPersistence StubPersistence()
    {
        return new InMemoryPersistence();
    }

    private sealed class InMemoryPersistence : IRegionPersistence
    {
        private readonly List<RegionDefinition> _regions = new();

        public ValueTask<IEnumerable<RegionDefinition>> LoadAsync() => ValueTask.FromResult<IEnumerable<RegionDefinition>>(_regions);
        public ValueTask SaveAsync(IEnumerable<RegionDefinition> regions) { _regions.Clear(); _regions.AddRange(regions); return ValueTask.CompletedTask; }
        public ValueTask ExportAsync(ObjectTracker.UI.Desktop.Region.Model.CameraZoneId cameraZoneId, string filePath) => ValueTask.CompletedTask;
        public ValueTask ImportAsync(string filePath, ObjectTracker.UI.Desktop.Region.Model.CameraZoneId targetCameraZoneId) => ValueTask.CompletedTask;
    }
}
