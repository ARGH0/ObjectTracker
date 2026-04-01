using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ObjectTracker.Core.Domain;
using ObjectTracker.Core.Ports;

namespace ObjectTracker.UI.Desktop;

internal sealed class OverlaySettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _filePath;

    public OverlaySettingsStore()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var settingsFolder = Path.Combine(appDataPath, "ObjectTracker");
        _filePath = Path.Combine(settingsFolder, "overlay-settings.json");
    }

    public void LoadIntoPipeline(IPipelineController pipeline)
    {
        if (!File.Exists(_filePath))
        {
            return;
        }

        var json = File.ReadAllText(_filePath);
        var settings = JsonSerializer.Deserialize<OverlaySettingsDto>(json, JsonOptions);
        if (settings is null)
        {
            return;
        }

        pipeline.SetOverlayLineThickness(settings.LineThickness);

        foreach (var (kind, color) in settings.Colors)
        {
            pipeline.SetOverlayColor(kind, color.R, color.G, color.B);
        }
    }

    public void SaveFromPipeline(IPipelineController pipeline)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        Directory.CreateDirectory(directory);

        var dto = new OverlaySettingsDto
        {
            LineThickness = pipeline.OverlayLineThickness,
            Colors = pipeline.OverlayColors
                .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    pair => pair.Key,
                    pair => new RgbColorDto
                    {
                        R = pair.Value.R,
                        G = pair.Value.G,
                        B = pair.Value.B
                    },
                    StringComparer.OrdinalIgnoreCase)
        };

        var json = JsonSerializer.Serialize(dto, JsonOptions);
        File.WriteAllText(_filePath, json);
    }

    private sealed class OverlaySettingsDto
    {
        public int LineThickness { get; set; } = 2;
        public Dictionary<string, RgbColorDto> Colors { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class RgbColorDto
    {
        public byte R { get; set; }
        public byte G { get; set; }
        public byte B { get; set; }
    }
}
