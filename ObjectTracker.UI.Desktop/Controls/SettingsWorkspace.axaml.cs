using System;
using Avalonia.Controls;
using ObjectTracker.UI.Desktop.Enums;
using ObjectTracker.UI.Desktop.ViewModels;

namespace ObjectTracker.UI.Desktop;

public partial class SettingsWorkspace : UserControl
{
    public event EventHandler? SaveRequested;
    public event EventHandler? DiscardRequested;
    public event EventHandler? DraftChanged;

    public SettingsWorkspace()
    {
        InitializeComponent();
        DataContext = new SettingsWorkspaceViewModel();
        HookEvents();
    }

    private void HookEvents()
    {
        SaveSettingsButton.Click += (_, _) => SaveRequested?.Invoke(this, EventArgs.Empty);
        DiscardSettingsButton.Click += (_, _) => DiscardRequested?.Invoke(this, EventArgs.Empty);
        SettingsGridColumnsTextBox.TextChanged += (_, _) => DraftChanged?.Invoke(this, EventArgs.Empty);
        SettingsGridRowsTextBox.TextChanged += (_, _) => DraftChanged?.Invoke(this, EventArgs.Empty);
        SettingsMissingFrameBehaviorComboBox.SelectionChanged += (_, _) => DraftChanged?.Invoke(this, EventArgs.Empty);
    }

    public AppSettings ReadDraftSettings(AppSettings fallbackSettings)
    {
        var columns = ParseInt(SettingsGridColumnsTextBox.Text, fallbackSettings.GridColumns, AppSettings.MinGridColumns, AppSettings.MaxGridColumns);
        var rows = ParseInt(SettingsGridRowsTextBox.Text, fallbackSettings.GridRows, AppSettings.MinGridRows, AppSettings.MaxGridRows);
        return new AppSettings(columns, rows, GetSelectedMissingFrameBehavior());
    }

    public CameraTileMissingFrameBehavior GetSelectedMissingFrameBehavior()
    {
        return SettingsMissingFrameBehaviorComboBox.SelectedIndex == 1
            ? CameraTileMissingFrameBehavior.BlackFrame
            : CameraTileMissingFrameBehavior.RepeatLastFrame;
    }

    private static int ParseInt(string? text, int fallback, int min, int max)
    {
        if (!int.TryParse(text, out var parsed))
        {
            parsed = fallback;
        }

        return Math.Clamp(parsed, min, max);
    }

    public void RefreshSettingsUi(AppSettings savedSettings, AppSettings draftSettings, bool hasPendingRestart, Func<SettingsField, string> getPolicyLabel)
    {
        if (DataContext is not SettingsWorkspaceViewModel viewModel)
        {
            return;
        }

        viewModel.GridColumnsPolicy = getPolicyLabel(SettingsField.GridColumns);
        viewModel.GridRowsPolicy = getPolicyLabel(SettingsField.GridRows);
        viewModel.MissingFrameBehaviorPolicy = getPolicyLabel(SettingsField.MissingFrameBehavior);
        viewModel.DraftStatus = MainWindow.BuildSettingsDraftState(savedSettings, draftSettings).StatusText;
        viewModel.PendingRestartStatus = hasPendingRestart ? "Pending restart: required" : "Pending restart: none";
        viewModel.CanSave = MainWindow.BuildSettingsDraftState(savedSettings, draftSettings).HasUnsavedChanges;
        viewModel.CanDiscard = MainWindow.BuildSettingsDraftState(savedSettings, draftSettings).HasUnsavedChanges;
    }

    public void RefreshDraftStatusUi(AppSettings savedSettings, AppSettings draftSettings, bool hasPendingRestart)
    {
        if (DataContext is not SettingsWorkspaceViewModel viewModel)
        {
            return;
        }

        var state = MainWindow.BuildSettingsDraftState(savedSettings, draftSettings);
        viewModel.DraftStatus = state.StatusText;
        viewModel.PendingRestartStatus = hasPendingRestart ? "Pending restart: required" : "Pending restart: none";
        viewModel.CanSave = state.HasUnsavedChanges;
        viewModel.CanDiscard = state.HasUnsavedChanges;
    }
}
