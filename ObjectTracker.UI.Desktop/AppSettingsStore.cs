using System;
using System.IO;
using System.Text.Json;
using ObjectTracker.UI.Desktop.Plc.Model;

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
            AdaptiveBackgroundSampleCount: ReadAdaptiveBackgroundSampleCount(dto),
            AdaptiveBackgroundUpdateIntervalFrames: ReadAdaptiveBackgroundUpdateIntervalFrames(dto),
            Plc: ReadPlcSettings(dto.Plc));
    }

    public void Save(AppSettings settings)
    {
        var dto = new AppSettingsDto
        {
            GridColumns = Math.Clamp(settings.GridColumns, AppSettings.MinGridColumns, AppSettings.MaxGridColumns),
            GridRows = Math.Clamp(settings.GridRows, AppSettings.MinGridRows, AppSettings.MaxGridRows),
            AdaptiveBackgroundSampleCount = Math.Max(
                AppSettings.MinAdaptiveBackgroundSampleCount,
                settings.AdaptiveBackgroundSampleCount),
            AdaptiveBackgroundUpdateIntervalFrames = Math.Max(
                AppSettings.MinAdaptiveBackgroundUpdateIntervalFrames,
                settings.AdaptiveBackgroundUpdateIntervalFrames),
            Plc = WritePlcSettings(settings.Plc)
        };

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(dto, JsonOptions);
        File.WriteAllText(filePath, json);
    }

    private static PlcSettings ReadPlcSettings(PlcSettingsDto? dto)
    {
        if (dto is null)
            return PlcSettings.Default;
        return new PlcSettings(
            BaseUrl: dto.BaseUrl ?? PlcSettings.DefaultBaseUrl,
            User: dto.User ?? PlcSettings.DefaultUser,
            Password: dto.Password ?? PlcSettings.DefaultPassword);
    }

    private static PlcSettingsDto WritePlcSettings(PlcSettings settings)
    {
        return new PlcSettingsDto
        {
            BaseUrl = settings.BaseUrl,
            User = settings.User,
            Password = settings.Password
        };
    }

    private static int ReadAdaptiveBackgroundSampleCount(AppSettingsDto dto)
        => Math.Max(AppSettings.MinAdaptiveBackgroundSampleCount,
            dto.AdaptiveBackgroundSampleCount ?? AppSettings.DefaultAdaptiveBackgroundSampleCount);

    private static int ReadAdaptiveBackgroundUpdateIntervalFrames(AppSettingsDto dto)
        => Math.Max(AppSettings.MinAdaptiveBackgroundUpdateIntervalFrames,
            dto.AdaptiveBackgroundUpdateIntervalFrames ?? AppSettings.DefaultAdaptiveBackgroundUpdateIntervalFrames);

    private sealed class AppSettingsDto
    {
        public int GridColumns { get; set; } = AppSettings.DefaultColumns;

        public int GridRows { get; set; } = AppSettings.DefaultRows;

        public int? AdaptiveBackgroundSampleCount { get; set; }

        public int? AdaptiveBackgroundUpdateIntervalFrames { get; set; }

        public PlcSettingsDto? Plc { get; set; }
    }

    private sealed class PlcSettingsDto
    {
        public string? BaseUrl { get; set; }

        public string? User { get; set; }

        public string? Password { get; set; }
    }
}

public readonly record struct AppSettings(
    int GridColumns,
    int GridRows,
    int AdaptiveBackgroundSampleCount,
    int AdaptiveBackgroundUpdateIntervalFrames,
    PlcSettings Plc)
{
    public const int DefaultColumns = 32;
    public const int DefaultRows = 18;
    public const int DefaultAdaptiveBackgroundSampleCount = 30;
    public const int DefaultAdaptiveBackgroundUpdateIntervalFrames = 30;
    public const int MinGridColumns = 2;
    public const int MaxGridColumns = 200;
    public const int MinGridRows = 2;
    public const int MaxGridRows = 200;
    public const int MinAdaptiveBackgroundSampleCount = 1;
    public const int MinAdaptiveBackgroundUpdateIntervalFrames = 1;

    public static AppSettings Default => new(
        DefaultColumns,
        DefaultRows,
        DefaultAdaptiveBackgroundSampleCount,
        DefaultAdaptiveBackgroundUpdateIntervalFrames,
        PlcSettings.Default);

    public AppSettings(int GridColumns, int GridRows)
        : this(
            GridColumns,
            GridRows,
            DefaultAdaptiveBackgroundSampleCount,
            DefaultAdaptiveBackgroundUpdateIntervalFrames,
            PlcSettings.Default)
    {
    }

    public AppSettings(int GridColumns, int GridRows, PlcSettings Plc)
        : this(
            GridColumns,
            GridRows,
            DefaultAdaptiveBackgroundSampleCount,
            DefaultAdaptiveBackgroundUpdateIntervalFrames,
            Plc)
    {
    }
}

public readonly record struct PlcSettings(string BaseUrl, string User, string Password)
{
    public const string DefaultBaseUrl = "https://192.168.0.190";
    public const string DefaultUser = "admin";
    public const string DefaultPassword = "Sander001!";

    public static PlcSettings Default => new(DefaultBaseUrl, DefaultUser, DefaultPassword);

    public PlcClientConfig ToClientConfig() => new(BaseUrl, User, Password);
}
