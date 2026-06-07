using System;
using System.IO;
using ObjectTracker.UI.Desktop.Enums;
using Xunit;

namespace ObjectTracker.UI.Desktop.Tests;

public sealed class AppSettingsStoreTests
{
    /// <summary>
    /// <description>Feature: AppSettingsStore returns default grid settings when the settings file is missing.
    /// 
    ///   Scenario: Loading from a non-existent path produces sensible defaults for grid dimensions and missing frame behavior.
    ///     Given an AppSettingsStore initialized with a temporary file path that does not exist,
    ///     When Load() is called,
    ///     Then GridColumns should be 32,
    ///      And GridRows should be 18,
    ///      And MissingFrameBehavior should be RepeatLastFrame.</description>
    /// </summary>
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

    /// <summary>
    /// <description>Feature: AppSettingsStore persists grid settings through save and load cycle.
    /// 
    ///   Scenario: Saving custom grid dimensions and reloading produces the same values.
    ///     Given an AppSettingsStore initialized with a temporary file path,
    ///     When Save(new AppSettings(GridColumns: 40, GridRows: 24)) is called,
    ///      And Load() is called to reload settings,
    ///     Then loaded.GridColumns should be 40,
    ///      And loaded.GridRows should be 24.</description>
    /// </summary>
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

    /// <summary>
    /// <description>Feature: AppSettingsStore persists missing frame behavior through save and load cycle.
    /// 
    ///   Scenario: Saving BlackFrame behavior and reloading produces the same value.
    ///     Given an AppSettingsStore initialized with a temporary file path,
    ///     When Save(new AppSettings(GridColumns: 40, GridRows: 24, MissingFrameBehavior: CameraTileMissingFrameBehavior.BlackFrame)) is called,
    ///      And Load() is called to reload settings,
    ///     Then loaded.MissingFrameBehavior should be BlackFrame.</description>
    /// </summary>
    [Fact]
    public void Save_ThenLoad_PersistsMissingFrameBehavior()
    {
        var path = BuildTempFilePath();

        var store = new AppSettingsStore(path);
        store.Save(new AppSettings(GridColumns: 40, GridRows: 24, MissingFrameBehavior: CameraTileMissingFrameBehavior.BlackFrame));

        var loaded = store.Load();

        Assert.Equal(CameraTileMissingFrameBehavior.BlackFrame, loaded.MissingFrameBehavior);
    }

    /// <summary>
    /// <description>Feature: AppSettingsStore clamps grid settings when values are out of range.
    /// 
    ///   Scenario: Saving GridColumns=0 and GridRows=999 (both outside valid ranges) produces clamped values on reload.
    ///     Given an AppSettingsStore initialized with a temporary file path,
    ///     When Save(new AppSettings(GridColumns: 0, GridRows: 999)) is called,
    ///      And Load() is called to reload settings,
    ///     Then loaded.GridColumns should be clamped to 2 (minimum),
    ///      And loaded.GridRows should be clamped to 200 (maximum).</description>
    /// </summary>
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
