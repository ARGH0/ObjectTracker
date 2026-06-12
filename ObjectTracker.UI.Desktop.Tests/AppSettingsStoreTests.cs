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
    }

    [Fact]
    public void Load_WhenAdaptiveBackgroundSettingsAreMissing_ReturnsDefaultAdaptiveBackgroundSettings()
    {
        var path = BuildTempFilePath();
        File.WriteAllText(path, """
        {
          "GridColumns": 40,
          "GridRows": 24
        }
        """);

        var store = new AppSettingsStore(path);

        var settings = store.Load();

        Assert.Equal(30, settings.AdaptiveBackgroundSampleCount);
        Assert.Equal(30, settings.AdaptiveBackgroundUpdateIntervalFrames);
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
    public void Save_ThenLoad_PersistsAdaptiveBackgroundSettings()
    {
        var path = BuildTempFilePath();

        var store = new AppSettingsStore(path);
        store.Save(new AppSettings(
            GridColumns: 40,
            GridRows: 24,
            AdaptiveBackgroundSampleCount: 12,
            AdaptiveBackgroundUpdateIntervalFrames: 90,
            PlcSettings.Default));

        var loaded = store.Load();

        Assert.Equal(12, loaded.AdaptiveBackgroundSampleCount);
        Assert.Equal(90, loaded.AdaptiveBackgroundUpdateIntervalFrames);
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

    [Fact]
    public void Load_WhenAdaptiveBackgroundValuesAreBelowMinimum_ClampsAdaptiveBackgroundSettings()
    {
        var path = BuildTempFilePath();
        File.WriteAllText(path, """
        {
          "GridColumns": 40,
          "GridRows": 24,
          "AdaptiveBackgroundSampleCount": 0,
          "AdaptiveBackgroundUpdateIntervalFrames": -5
        }
        """);

        var store = new AppSettingsStore(path);

        var loaded = store.Load();

        Assert.Equal(1, loaded.AdaptiveBackgroundSampleCount);
        Assert.Equal(1, loaded.AdaptiveBackgroundUpdateIntervalFrames);
    }

    private static string BuildTempFilePath()
    {
        var folder = Path.Combine(Path.GetTempPath(), "ObjectTracker.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, "app-settings.json");
    }
}
