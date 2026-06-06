using System;
using System.IO;
using Xunit;

namespace ObjectTracker.UI.Desktop.Tests;

public sealed class AppSettingsStoreTests
{
    [Fact]
    public void Load_WhenFileMissing_ReturnsDefaultGridSettings()
    {
        var path = BuildTempFilePath();

        var store = new AppSettingsStore(path);

        var settings = store.Load();

        Assert.Equal(32, settings.GridColumns);
        Assert.Equal(18, settings.GridRows);
        Assert.Equal(CameraTileMissingFrameBehavior.RepeatLastFrame, settings.MissingFrameBehavior);
    }

    [Fact]
    public void Save_ThenLoad_PersistsGridSettings()
    {
        var path = BuildTempFilePath();

        var store = new AppSettingsStore(path);
        store.Save(new AppSettings(GridColumns: 40, GridRows: 24));

        var loaded = store.Load();

        Assert.Equal(40, loaded.GridColumns);
        Assert.Equal(24, loaded.GridRows);
    }

    [Fact]
    public void Save_ThenLoad_PersistsMissingFrameBehavior()
    {
        var path = BuildTempFilePath();

        var store = new AppSettingsStore(path);
        store.Save(new AppSettings(GridColumns: 40, GridRows: 24, MissingFrameBehavior: CameraTileMissingFrameBehavior.BlackFrame));

        var loaded = store.Load();

        Assert.Equal(CameraTileMissingFrameBehavior.BlackFrame, loaded.MissingFrameBehavior);
    }

    [Fact]
    public void Load_WhenValuesOutOfRange_ClampsGridSettings()
    {
        var path = BuildTempFilePath();

        var store = new AppSettingsStore(path);
        store.Save(new AppSettings(GridColumns: 0, GridRows: 999));

        var loaded = store.Load();

        Assert.Equal(2, loaded.GridColumns);
        Assert.Equal(200, loaded.GridRows);
    }

    private static string BuildTempFilePath()
    {
        var folder = Path.Combine(Path.GetTempPath(), "ObjectTracker.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, "app-settings.json");
    }
}
