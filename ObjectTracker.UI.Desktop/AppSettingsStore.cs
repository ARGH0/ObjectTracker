using System;
using System.IO;
using System.Text.Json;
using ObjectTracker.UI.Desktop.Enums;

namespace ObjectTracker.UI.Desktop;

public sealed class AppSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string filePath;

    public AppSettingsStore(string? filePathOverride = null)
    {
        if (!string.IsNullOrWhiteSpace(filePathOverride))
        {
            filePath = filePathOverride;
            return;
        }

        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var settingsFolder = Path.Combine(appDataPath, "ObjectTracker");
        filePath = Path.Combine(settingsFolder, "app-settings.json");
    }

    public AppSettings Load()
    {
        if (!File.Exists(filePath))
        {
            return AppSettings.Default;
        }

        var json = File.ReadAllText(filePath);
        var dto = JsonSerializer.Deserialize<AppSettingsDto>(json, JsonOptions);
        if (dto is null)
        {
            return AppSettings.Default;
        }

        return new AppSettings(
            GridColumns: Math.Clamp(dto.GridColumns, AppSettings.MinGridColumns, AppSettings.MaxGridColumns),
            GridRows: Math.Clamp(dto.GridRows, AppSettings.MinGridRows, AppSettings.MaxGridRows),
            MissingFrameBehavior: dto.MissingFrameBehavior);
    }

    public void Save(AppSettings settings)
    {
        var dto = new AppSettingsDto
        {
            GridColumns = Math.Clamp(settings.GridColumns, AppSettings.MinGridColumns, AppSettings.MaxGridColumns),
            GridRows = Math.Clamp(settings.GridRows, AppSettings.MinGridRows, AppSettings.MaxGridRows),
            MissingFrameBehavior = settings.MissingFrameBehavior
        };

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(dto, JsonOptions);
        File.WriteAllText(filePath, json);
    }

    private sealed class AppSettingsDto
    {
        public int GridColumns { get; set; } = AppSettings.DefaultColumns;

        public int GridRows { get; set; } = AppSettings.DefaultRows;

        public CameraTileMissingFrameBehavior MissingFrameBehavior { get; set; } = AppSettings.DefaultMissingFrameBehavior;
    }
}

public readonly record struct AppSettings(
    int GridColumns,
    int GridRows,
    CameraTileMissingFrameBehavior MissingFrameBehavior = CameraTileMissingFrameBehavior.RepeatLastFrame)
{
    public const int DefaultColumns = 32;
    public const int DefaultRows = 18;
    public const CameraTileMissingFrameBehavior DefaultMissingFrameBehavior = CameraTileMissingFrameBehavior.RepeatLastFrame;
    public const int MinGridColumns = 2;
    public const int MaxGridColumns = 200;
    public const int MinGridRows = 2;
    public const int MaxGridRows = 200;

    public static AppSettings Default => new(DefaultColumns, DefaultRows, DefaultMissingFrameBehavior);
}
