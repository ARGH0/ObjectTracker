using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace ObjectTracker.UI.Desktop;

internal sealed class FileCameraSourceStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string filePath;

    public FileCameraSourceStore()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var settingsFolder = Path.Combine(appDataPath, "ObjectTracker");
        filePath = Path.Combine(settingsFolder, "file-camera-sources.json");
    }

    public FileCameraSourceStore(string filePath)
    {
        this.filePath = filePath;
    }

    public IReadOnlyList<MainWindow.FileCameraSource> Load()
    {
        if (!File.Exists(filePath))
        {
            return Array.Empty<MainWindow.FileCameraSource>();
        }

        var json = File.ReadAllText(filePath);
        var dto = JsonSerializer.Deserialize<FileCameraSourceFileDto>(json, JsonOptions);
        if (dto?.Sources is null)
        {
            return Array.Empty<MainWindow.FileCameraSource>();
        }

        var sources = new List<MainWindow.FileCameraSource>();
        foreach (var source in dto.Sources)
        {
            if (string.IsNullOrWhiteSpace(source.CameraId) || string.IsNullOrWhiteSpace(source.VideoPath))
            {
                continue;
            }

            sources.Add(new MainWindow.FileCameraSource(
                source.CameraId,
                source.DisplayName ?? string.Empty,
                source.VideoPath,
                source.LoopVideo));
        }

        return sources;
    }

    public void Save(IReadOnlyCollection<MainWindow.FileCameraSource> sources)
    {
        var dto = new FileCameraSourceFileDto
        {
            Sources = new List<FileCameraSourceDto>()
        };

        foreach (var source in sources)
        {
            dto.Sources.Add(new FileCameraSourceDto
            {
                CameraId = source.CameraId,
                DisplayName = source.DisplayName,
                VideoPath = source.VideoPath,
                LoopVideo = source.LoopVideo
            });
        }

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(dto, JsonOptions);
        File.WriteAllText(filePath, json);
    }

    private sealed class FileCameraSourceFileDto
    {
        public List<FileCameraSourceDto> Sources { get; set; } = new();
    }

    private sealed class FileCameraSourceDto
    {
        public string CameraId { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public string VideoPath { get; set; } = string.Empty;

        public bool LoopVideo { get; set; }
    }
}
