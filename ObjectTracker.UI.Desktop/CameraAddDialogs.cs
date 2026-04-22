using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using VideoCaptureAPIs = OpenCvSharp.VideoCaptureAPIs;

namespace ObjectTracker.UI.Desktop;

internal enum CameraAddChoice
{
    VideoFiles,
    UsbCamera
}

internal readonly record struct UsbCameraOption(string Id, string DisplayName, int CameraIndex, VideoCaptureAPIs Api);

internal sealed class CameraSourceChoiceDialog : Window
{
    public CameraSourceChoiceDialog()
    {
        Title = "Add Camera";
        Width = 520;
        Height = 310;
        CanResize = false;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.Parse("#090C10"));

        var eyebrow = new TextBlock
        {
            Text = "SOURCE SETUP",
            Foreground = new SolidColorBrush(Color.Parse("#9EB2C1")),
            FontSize = 11,
            FontWeight = FontWeight.SemiBold
        };

        var title = new TextBlock
        {
            Text = "Add a new camera feed",
            Foreground = new SolidColorBrush(Color.Parse("#F2F7FA")),
            FontSize = 24,
            FontWeight = FontWeight.Bold
        };

        var header = new TextBlock
        {
            Text = "Choose whether you want to work from recorded footage or a live USB device.",
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.Parse("#93A6B5")),
            FontSize = 14,
            LineHeight = 20
        };

        var videoButton = new Button
        {
            Content = "Use Video Files",
            Classes = { "primary" },
            Height = 48,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        videoButton.Click += (_, _) => Close(CameraAddChoice.VideoFiles);

        var videoHint = new TextBlock
        {
            Text = "Best when you want stable repeatable testing with the same clips.",
            Foreground = new SolidColorBrush(Color.Parse("#93A6B5")),
            FontSize = 12,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap
        };

        var usbButton = new Button
        {
            Content = "Use USB Camera",
            Classes = { "ghost" },
            Height = 48,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        usbButton.Click += (_, _) => Close(CameraAddChoice.UsbCamera);

        var usbHint = new TextBlock
        {
            Text = "Best for live tuning and quick validation with an attached device.",
            Foreground = new SolidColorBrush(Color.Parse("#93A6B5")),
            FontSize = 12,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap
        };

        var cancelButton = new Button
        {
            Content = "Cancel",
            Classes = { "ghost" },
            Height = 38,
            HorizontalAlignment = HorizontalAlignment.Right,
            MinWidth = 100
        };
        cancelButton.Click += (_, _) => Close();

        Content = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#11161C")),
            BorderBrush = new SolidColorBrush(Color.Parse("#28333D")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(18),
            Margin = new Thickness(18),
            Padding = new Thickness(22),
            Child = new StackPanel
            {
                Spacing = 16,
                Children =
                {
                    eyebrow,
                    title,
                    header,
                    new Border
                    {
                        Background = new SolidColorBrush(Color.Parse("#161D25")),
                        BorderBrush = new SolidColorBrush(Color.Parse("#28333D")),
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(14),
                        Padding = new Thickness(14),
                        Child = new StackPanel
                        {
                            Spacing = 8,
                            Children = { videoButton, videoHint }
                        }
                    },
                    new Border
                    {
                        Background = new SolidColorBrush(Color.Parse("#161D25")),
                        BorderBrush = new SolidColorBrush(Color.Parse("#28333D")),
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(14),
                        Padding = new Thickness(14),
                        Child = new StackPanel
                        {
                            Spacing = 8,
                            Children = { usbButton, usbHint }
                        }
                    },
                    cancelButton
                }
            }
        };
    }
}

internal sealed class UsbCameraSelectionDialog : Window
{
    private readonly IReadOnlyList<UsbCameraOption> _options;
    private readonly ListBox _cameraListBox;

    public UsbCameraSelectionDialog(IReadOnlyList<UsbCameraOption> options)
    {
        _options = options;

        Title = "Select USB Camera";
        Width = 560;
        Height = 420;
        CanResize = false;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.Parse("#090C10"));

        var eyebrow = new TextBlock
        {
            Text = "DEVICE PICKER",
            Foreground = new SolidColorBrush(Color.Parse("#9EB2C1")),
            FontSize = 11,
            FontWeight = FontWeight.SemiBold
        };

        var title = new TextBlock
        {
            Text = "Select a USB camera",
            Foreground = new SolidColorBrush(Color.Parse("#F2F7FA")),
            FontSize = 24,
            FontWeight = FontWeight.Bold
        };

        var header = new TextBlock
        {
            Text = options.Count == 0
                ? "No USB cameras were detected on this machine right now."
                : "Choose the live camera source you want to add to the workspace.",
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.Parse("#93A6B5")),
            FontSize = 14,
            LineHeight = 20
        };

        _cameraListBox = new ListBox
        {
            Height = 220,
            ItemsSource = options.Select(option => option.DisplayName).ToList(),
            SelectedIndex = options.Count > 0 ? 0 : -1
        };

        var addButton = new Button
        {
            Content = "Add USB Camera",
            Classes = { "primary" },
            Height = 38,
            MinWidth = 140,
            IsEnabled = options.Count > 0
        };
        addButton.Click += (_, _) => ConfirmSelection();

        var cancelButton = new Button
        {
            Content = "Cancel",
            Classes = { "ghost" },
            Height = 38,
            MinWidth = 100
        };
        cancelButton.Click += (_, _) => Close();

        Content = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#11161C")),
            BorderBrush = new SolidColorBrush(Color.Parse("#28333D")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(18),
            Margin = new Thickness(18),
            Padding = new Thickness(22),
            Child = new StackPanel
            {
                Spacing = 16,
                Children =
                {
                    eyebrow,
                    title,
                    header,
                    new Border
                    {
                        Background = new SolidColorBrush(Color.Parse("#161D25")),
                        BorderBrush = new SolidColorBrush(Color.Parse("#28333D")),
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(14),
                        Padding = new Thickness(12),
                        Child = _cameraListBox
                    },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Spacing = 10,
                        Children =
                        {
                            addButton,
                            cancelButton
                        }
                    }
                }
            }
        };
    }

    private void ConfirmSelection()
    {
        var selectedIndex = _cameraListBox.SelectedIndex;
        if (selectedIndex < 0 || selectedIndex >= _options.Count)
        {
            return;
        }

        Close(_options[selectedIndex]);
    }
}