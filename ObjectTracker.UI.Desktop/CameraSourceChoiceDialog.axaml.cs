using Avalonia.Controls;

namespace ObjectTracker.UI.Desktop;

internal partial class CameraSourceChoiceDialog : Window
{
    public CameraSourceChoiceDialog()
    {
        InitializeComponent();
        VideoFilesButton.Click += (_, _) => Close(CameraAddChoice.VideoFiles);
        UsbCameraButton.Click  += (_, _) => Close(CameraAddChoice.UsbCamera);
        CancelButton.Click     += (_, _) => Close();
    }
}
