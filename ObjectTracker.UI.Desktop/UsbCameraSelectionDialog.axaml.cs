using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;

namespace ObjectTracker.UI.Desktop;

internal partial class UsbCameraSelectionDialog : Window
{
    private readonly IReadOnlyList<UsbCameraOption> _options;

    public UsbCameraSelectionDialog(IReadOnlyList<UsbCameraOption> options)
    {
        _options = options;
        InitializeComponent();

        HeaderText.Text = options.Count == 0
            ? "No USB cameras were detected on this machine right now."
            : "Choose the live camera source you want to add to the workspace.";

        CameraListBox.ItemsSource = options.Select(o => o.DisplayName).ToList();
        CameraListBox.SelectedIndex = options.Count > 0 ? 0 : -1;
        AddButton.IsEnabled = options.Count > 0;

        AddButton.Click  += (_, _) => ConfirmSelection();
        CancelButton.Click += (_, _) => Close();
    }

    private void ConfirmSelection()
    {
        var idx = CameraListBox.SelectedIndex;
        if (idx < 0 || idx >= _options.Count) return;
        Close(_options[idx]);
    }
}
