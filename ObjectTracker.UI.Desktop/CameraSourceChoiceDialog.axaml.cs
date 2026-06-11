using Avalonia.Controls;

namespace ObjectTracker.UI.Desktop;

internal partial class CameraSourceChoiceDialog : Window
{
    public CameraSourceChoiceDialog()
    {
        InitializeComponent();
        VideoFilesButton.Click += (_, _) => Close(CameraAddChoice.VideoFiles);
        CancelButton.Click += (_, _) => Close();
    }
}
