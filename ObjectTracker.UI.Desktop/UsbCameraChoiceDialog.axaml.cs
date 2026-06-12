using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ObjectTracker.UI.Desktop;

namespace ObjectTracker.UI.Desktop;

internal partial class UsbCameraChoiceDialog : Window
{
    private UsbCameraDevice? _selectedDevice;

    public UsbCameraChoiceDialog()
    {
        InitializeComponent();

        var devices = UsbCameraDeviceEnumerator.Enumerate().ToList();

        if (devices.Count == 0)
        {
            StatusText.Text = "No USB cameras found.";
            ConfirmButton.IsEnabled = false;
        }
        else
        {
            DeviceList.ItemsSource = devices.Select(d => d.DisplayName).ToList();
            DeviceList.SelectionChanged += (_, _) => OnDeviceSelectionChanged(devices);
            OnDeviceSelectionChanged(devices);
        }

        ConfirmButton.Click += (_, _) => Close(_selectedDevice);
        CancelButton.Click += (_, _) => Close(null);
    }

    private void OnDeviceSelectionChanged(IReadOnlyList<UsbCameraDevice> devices)
    {
        var selectedIndex = DeviceList.SelectedIndex;
        if (selectedIndex >= 0 && selectedIndex < devices.Count)
        {
            _selectedDevice = devices[selectedIndex];
        }
        else
        {
            _selectedDevice = null;
        }

        ConfirmButton.IsEnabled = _selectedDevice.HasValue;
    }

    private void RefreshButtonOnClick(object? sender, RoutedEventArgs e)
    {
        // Intentionally left blank for future hot-plug support.
    }
}
