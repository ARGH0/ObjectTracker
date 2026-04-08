using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using ObjectTracker.Core.Ports;

namespace ObjectTracker.UI.Desktop;

public partial class OverlaySettingsWindow : Window
{
    private readonly IPipelineController _pipeline;
    private readonly OverlaySettingsStore _settingsStore;

    public OverlaySettingsWindow()
        : this(ResolveDependencies())
    {
    }

    private OverlaySettingsWindow((IPipelineController Pipeline, OverlaySettingsStore Store) dependencies)
        : this(dependencies.Pipeline, dependencies.Store)
    {
    }

    internal OverlaySettingsWindow(IPipelineController pipeline, OverlaySettingsStore settingsStore)
    {
        _pipeline = pipeline;
        _settingsStore = settingsStore;

        InitializeComponent();
        BindData();
        HookEvents();
    }

    private static (IPipelineController Pipeline, OverlaySettingsStore Store) ResolveDependencies()
    {
        if (App.Services is not null)
        {
            return (
                App.Services.GetRequiredService<IPipelineController>(),
                new OverlaySettingsStore());
        }

        var dependencies = DesktopCompositionRoot.CreateDependencies();
        return (dependencies.Pipeline, new OverlaySettingsStore());
    }

    private void BindData()
    {
        OverlayThicknessTextBox.Text = _pipeline.OverlayLineThickness.ToString();

        var kinds = _pipeline.OverlayColors.Keys
            .OrderBy(kind => kind)
            .ToList();

        OverlayKindComboBox.ItemsSource = kinds;
        OverlayKindComboBox.SelectedItem = kinds.FirstOrDefault();

        LoadSelectedColor();
        StatusText.Text = "";
    }

    private void HookEvents()
    {
        OverlayKindComboBox.SelectionChanged += OverlayKindComboBoxOnSelectionChanged;
        ApplyButton.Click += ApplyButtonOnClick;
        ResetDefaultsButton.Click += ResetDefaultsButtonOnClick;
        CloseButton.Click += CloseButtonOnClick;
    }

    private void OverlayKindComboBoxOnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        LoadSelectedColor();
    }

    private void ApplyButtonOnClick(object? sender, RoutedEventArgs e)
    {
        if (!int.TryParse(OverlayThicknessTextBox.Text, out var thickness))
        {
            StatusText.Text = "Ongeldige lijndikte. Gebruik 1-10.";
            return;
        }

        thickness = Math.Clamp(thickness, 1, 10);
        _pipeline.SetOverlayLineThickness(thickness);
        OverlayThicknessTextBox.Text = thickness.ToString();

        if (OverlayKindComboBox.SelectedItem is not string kind || string.IsNullOrWhiteSpace(kind))
        {
            StatusText.Text = "Kies eerst een kleur kind.";
            return;
        }

        if (!TryParseRgbValue(OverlayRedTextBox.Text, out var r) ||
            !TryParseRgbValue(OverlayGreenTextBox.Text, out var g) ||
            !TryParseRgbValue(OverlayBlueTextBox.Text, out var b))
        {
            StatusText.Text = "RGB waardes moeten 0-255 zijn.";
            return;
        }

        _pipeline.SetOverlayColor(kind, r, g, b);

        try
        {
            _settingsStore.SaveFromPipeline(_pipeline);
            StatusText.Text = $"Opgeslagen: {kind} = ({r},{g},{b}), dikte {thickness}";
        }
        catch
        {
            StatusText.Text = "Opslaan mislukt.";
        }
    }

    private void ResetDefaultsButtonOnClick(object? sender, RoutedEventArgs e)
    {
        _pipeline.ResetOverlaySettings();

        try
        {
            _settingsStore.SaveFromPipeline(_pipeline);
            BindData();
            StatusText.Text = "Defaults hersteld en opgeslagen.";
        }
        catch
        {
            StatusText.Text = "Defaults hersteld, maar opslaan mislukt.";
        }
    }

    private void CloseButtonOnClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void LoadSelectedColor()
    {
        if (OverlayKindComboBox.SelectedItem is not string kind ||
            !_pipeline.OverlayColors.TryGetValue(kind, out var rgb))
        {
            return;
        }

        OverlayRedTextBox.Text = rgb.R.ToString();
        OverlayGreenTextBox.Text = rgb.G.ToString();
        OverlayBlueTextBox.Text = rgb.B.ToString();
    }

    private static bool TryParseRgbValue(string? text, out byte value)
    {
        value = 0;
        return byte.TryParse(text, out value);
    }
}
