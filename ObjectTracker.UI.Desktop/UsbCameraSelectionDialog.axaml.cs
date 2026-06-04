using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;

namespace ObjectTracker.UI.Desktop;

internal partial class UsbCameraSelectionDialog : Window
{
    private readonly IReadOnlyList<UsbCameraOption> options;

    public UsbCameraSelectionDialog(IReadOnlyList<UsbCameraOption> options)
    {
        this.options = options;
        InitializeComponent();

        HeaderText.Text = options.Count == 0
            ? "No USB cameras were detected on this machine right now."
            : "Choose the live camera source you want to add to the workspace.";

        CameraListBox.ItemsSource = options.Select(o => o.IsAvailable ? o.DisplayName : $"{o.DisplayName} - {o.Status}").ToList();
        CameraListBox.SelectedIndex = options.ToList().FindIndex(option => option.IsAvailable);
        AddButton.IsEnabled = CameraListBox.SelectedIndex >= 0;
        CameraListBox.SelectionChanged += (_, _) =>
        {
            AddButton.IsEnabled = UsbCameraSelectionPolicy.ResolveSelectableOption(options, CameraListBox.SelectedIndex) is not null;
        };

        AddButton.Click += (_, _) => ConfirmSelection();
        CancelButton.Click += (_, _) => Close();
    }

    private void ConfirmSelection()
    {
        var idx = CameraListBox.SelectedIndex;
        if (idx < 0 || idx >= options.Count)
        {
            return;
        }

        var selected = UsbCameraSelectionPolicy.ResolveSelectableOption(options, idx);
        if (selected is null)
        {
            return;
        }

        Close(selected.Value);
    }
}
