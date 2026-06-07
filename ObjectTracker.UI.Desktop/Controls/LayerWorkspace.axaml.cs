using System;
using System.Collections;
using System.Collections.Generic;
using Avalonia.Controls;
using ObjectTracker.UI.Desktop.ViewModels;

namespace ObjectTracker.UI.Desktop;

public partial class LayerWorkspace : UserControl
{
    public event EventHandler? AddLayerTypeRequested;
    public event EventHandler? DeleteLayerTypeRequested;
    public event EventHandler? LayerTypeSelectionChanged;

    public LayerWorkspace()
    {
        InitializeComponent();
        DataContext = new LayerWorkspaceViewModel();
        HookEvents();
    }

    private void HookEvents()
    {
        AddGlobalLayerTypeButton.Click += (_, _) => AddLayerTypeRequested?.Invoke(this, EventArgs.Empty);
        DeleteGlobalLayerTypeButton.Click += (_, _) => DeleteLayerTypeRequested?.Invoke(this, EventArgs.Empty);
        GlobalLayerTypesListBox.SelectionChanged += (_, _) => LayerTypeSelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public (string Id, string Name, string Precedence) ReadLayerTypeDraft()
    {
        return (
            GlobalLayerTypeIdTextBox.Text ?? string.Empty,
            GlobalLayerTypeNameTextBox.Text ?? string.Empty,
            GlobalLayerTypePrecedenceTextBox.Text ?? string.Empty);
    }

    public void SetLayerTypeDraft(string id, string name, string precedence)
    {
        GlobalLayerTypeIdTextBox.Text = id;
        GlobalLayerTypeNameTextBox.Text = name;
        GlobalLayerTypePrecedenceTextBox.Text = precedence;
    }

    public void SetLayerTypeItems(IEnumerable? items)
    {
        GlobalLayerTypesListBox.ItemsSource = items;
    }

    public void SetLayerTypeSelection(int index)
    {
        GlobalLayerTypesListBox.SelectedIndex = index;
    }

    public string? SelectedLayerTypeId => GetSelectedItemValue<string>("LayerTypeId");

    public string? SelectedLayerTypeDisplayName => GetSelectedItemValue<string>("DisplayName");

    public void SetUsageItems(IEnumerable<string>? items)
    {
        GlobalLayerTypeUsageListBox.ItemsSource = items;
    }

    public void SetDeleteStatus(string message, bool canDelete)
    {
        GlobalLayerTypeDeleteStatusText.Text = message;
        DeleteGlobalLayerTypeButton.IsEnabled = canDelete;
    }

    private T? GetSelectedItemValue<T>(string propertyName)
    {
        if (GlobalLayerTypesListBox.SelectedItem is null)
        {
            return default;
        }

        var property = GlobalLayerTypesListBox.SelectedItem.GetType().GetProperty(propertyName);
        return property?.GetValue(GlobalLayerTypesListBox.SelectedItem) is T value
            ? value
            : default;
    }
}
