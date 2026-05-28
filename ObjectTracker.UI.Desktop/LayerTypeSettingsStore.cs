using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace ObjectTracker.UI.Desktop;

public sealed class LayerTypeSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string filePath;

    public LayerTypeSettingsStore(string? filePathOverride = null)
    {
        if (!string.IsNullOrWhiteSpace(filePathOverride))
        {
            filePath = filePathOverride;
            return;
        }

        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var settingsFolder = Path.Combine(appDataPath, "ObjectTracker");
        filePath = Path.Combine(settingsFolder, "layer-types.json");
    }

    public LayerTypeCatalogService Load()
    {
        if (!File.Exists(filePath))
        {
            return LayerTypeCatalogService.CreateDefault();
        }

        var json = File.ReadAllText(filePath);
        var dto = JsonSerializer.Deserialize<LayerTypeSettingsDto>(json, JsonOptions);
        if (dto?.Items is null || dto.Items.Count == 0)
        {
            return LayerTypeCatalogService.CreateDefault();
        }

        var definitions = new List<LayerTypeDefinition>(dto.Items.Count);
        foreach (var item in dto.Items)
        {
            definitions.Add(new LayerTypeDefinition(
                item.LayerTypeId ?? string.Empty,
                item.DisplayName ?? string.Empty,
                item.Precedence,
                ParseMergePolicy(item.MergePolicy),
                ParseBehaviorClass(item.BehaviorClass)));
        }

        return LayerTypeCatalogService.Create(definitions);
    }

    public void Save(IReadOnlyCollection<LayerTypeDefinition> definitions)
    {
        var dto = new LayerTypeSettingsDto();
        foreach (var definition in definitions)
        {
            dto.Items.Add(new LayerTypeItemDto
            {
                LayerTypeId = definition.LayerTypeId,
                DisplayName = definition.DisplayName,
                Precedence = definition.Precedence,
                MergePolicy = definition.MergePolicy.ToString(),
                BehaviorClass = definition.BehaviorClass.ToString()
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

    private static LayerMergePolicy ParseMergePolicy(string? value)
    {
        return Enum.TryParse<LayerMergePolicy>(value, ignoreCase: true, out var parsed)
            ? parsed
            : LayerMergePolicy.PreserveRegions;
    }

    private static LayerTypeBehaviorClass ParseBehaviorClass(string? value)
    {
        return Enum.TryParse<LayerTypeBehaviorClass>(value, ignoreCase: true, out var parsed)
            ? parsed
            : LayerTypeBehaviorClass.Informational;
    }

    private sealed class LayerTypeSettingsDto
    {
        public List<LayerTypeItemDto> Items { get; set; } = new();
    }

    private sealed class LayerTypeItemDto
    {
        public string LayerTypeId { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public int Precedence { get; set; }

        public string MergePolicy { get; set; } = nameof(LayerMergePolicy.PreserveRegions);

        public string BehaviorClass { get; set; } = nameof(LayerTypeBehaviorClass.Informational);
    }
}
