using Microsoft.Extensions.DependencyInjection;
using ObjectTracker.UI.Desktop.Region.Contracts;
using ObjectTracker.UI.Desktop.Region.Model;
using GridEditorDialog = ObjectTracker.UI.Desktop.Region.Implementation.GridEditorDialog;
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
        Assert.NotNull(provider.GetRequiredService<IRegionPriorityResolver>());
        Assert.NotNull(provider.GetRequiredService<IRegionPersistence>());
        Assert.NotNull(provider.GetRequiredService<IRegionRegistry>());
        Assert.NotNull(provider.GetRequiredService<IHandoffResolver>());
        Assert.NotNull(provider.GetRequiredService<IRegionEvaluator>());
        Assert.NotNull(provider.GetRequiredService<IRegionProcessorService>());
        Assert.NotNull(provider.GetRequiredService<IRegionManagerService>());
        Assert.NotNull(provider.GetRequiredService<GridEditorDialog>());
    }

    [Fact]
    public void AddRegionServices_ResolvesAllServicesWithoutErrors()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IRegionPersistence>(StubPersistence());
        services.AddRegionServices();

        var provider = services.BuildServiceProvider();

        var coordinatorMapper = provider.GetService<ICoordinateMapper>();
        var priorityResolver = provider.GetService<IRegionPriorityResolver>();
        var persistence = provider.GetService<IRegionPersistence>();
        var registry = provider.GetService<IRegionRegistry>();
        var handoffResolver = provider.GetService<IHandoffResolver>();
        var evaluator = provider.GetService<IRegionEvaluator>();
        var processorService = provider.GetService<IRegionProcessorService>();
        var managerService = provider.GetService<IRegionManagerService>();
        var gridEditorDialog = provider.GetService<GridEditorDialog>();

        Assert.NotNull(coordinatorMapper);
        Assert.NotNull(priorityResolver);
        Assert.NotNull(persistence);
        Assert.NotNull(registry);
        Assert.NotNull(handoffResolver);
        Assert.NotNull(evaluator);
        Assert.NotNull(processorService);
        Assert.NotNull(managerService);
        Assert.NotNull(gridEditorDialog);
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
    public void RegionPriorityResolver_IsSingleton()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IRegionPersistence>(StubPersistence());
        services.AddRegionServices();

        var provider = services.BuildServiceProvider();

        var first = provider.GetRequiredService<IRegionPriorityResolver>();
        var second = provider.GetRequiredService<IRegionPriorityResolver>();

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

    [Fact]
    public void GridEditorDialog_IsTransient()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IRegionPersistence>(StubPersistence());
        services.AddRegionServices();

        var provider = services.BuildServiceProvider();

        var first = provider.GetRequiredService<GridEditorDialog>();
        var second = provider.GetRequiredService<GridEditorDialog>();

        Assert.NotSame(first, second);
    }

    private static IRegionPersistence StubPersistence()
    {
        return new InMemoryPersistence();
    }

    private sealed class InMemoryPersistence : IRegionPersistence
    {
        private readonly List<RegionDefinition> _regions = new();

        public IEnumerable<RegionDefinition> Load() => _regions;
        public void Save(IEnumerable<RegionDefinition> regions) { _regions.Clear(); _regions.AddRange(regions); }
    }
}
