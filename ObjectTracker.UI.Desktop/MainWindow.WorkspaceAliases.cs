using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace ObjectTracker.UI.Desktop;

public partial class MainWindow
{
    private ListBox LogListBox => CameraWorkspaceControl.LogListBox;
    private Button AddVideosButton => CameraWorkspaceControl.AddVideosButton;
    private Button RemoveSelectedButton => CameraWorkspaceControl.RemoveSelectedButton;
    private MenuItem DeleteSelectedCameraMenuItem => CameraWorkspaceControl.DeleteSelectedCameraMenuItem;
    private Button MoveCameraUpButton => CameraWorkspaceControl.MoveCameraUpButton;
    private Button MoveCameraDownButton => CameraWorkspaceControl.MoveCameraDownButton;
    private Button PreviousVideoButton => CameraWorkspaceControl.PreviousVideoButton;
    private Button NextVideoButton => CameraWorkspaceControl.NextVideoButton;

    private Button OpenBakedMaskButton => CameraWorkspaceControl.OpenBakedMaskButton;
    private ListBox CameraSourceListBox => CameraWorkspaceControl.CameraSourceListBox;
    private ComboBox CameraZoneComboBox => CameraWorkspaceControl.CameraZoneComboBox;
    private CheckBox CameraVisibilityCheckBox => CameraWorkspaceControl.CameraVisibilityCheckBox;
    private CheckBox VisionPipelineInclusionCheckBox => CameraWorkspaceControl.VisionPipelineInclusionCheckBox;
    private CheckBox CameraDebugViewCheckBox => CameraWorkspaceControl.CameraDebugViewCheckBox;
    private Button RestartUsbCameraSourceButton => CameraWorkspaceControl.RestartUsbCameraSourceButton;
    private ComboBox UsbResolutionComboBox => CameraWorkspaceControl.UsbResolutionComboBox;
    private ComboBox UsbTargetFpsComboBox => CameraWorkspaceControl.UsbTargetFpsComboBox;
    private Button ApplyUsbCaptureSettingsButton => CameraWorkspaceControl.ApplyUsbCaptureSettingsButton;
    private Button RevertUsbCaptureSettingsButton => CameraWorkspaceControl.RevertUsbCaptureSettingsButton;
    private Button ToggleGridEditorButton => CameraWorkspaceControl.ToggleGridEditorButton;
    private Button AddLayerButton => CameraWorkspaceControl.AddLayerButton;
    private Button DeleteLayerButton => CameraWorkspaceControl.DeleteLayerButton;

    private ListBox LayersListBox => CameraWorkspaceControl.LayersListBox;
    private ListBox RegionsListBox => CameraWorkspaceControl.RegionsListBox;
    private Button SaveRegionButton => CameraWorkspaceControl.SaveRegionButton;
    private Button DeleteRegionButton => CameraWorkspaceControl.DeleteRegionButton;

    private ComboBox BakeSourceComboBox => CameraWorkspaceControl.BakeSourceComboBox;
    private Button SelectBakeImageButton => CameraWorkspaceControl.SelectBakeImageButton;
    private Button ClearBakeImageButton => CameraWorkspaceControl.ClearBakeImageButton;
    private CheckBox LoopVideoCheckBox => CameraWorkspaceControl.LoopVideoCheckBox;
    private Button MarkAmbiguityButton => CameraWorkspaceControl.MarkAmbiguityButton;

    private UniformGrid CameraTileGrid => CameraWorkspaceControl.CameraTileGrid;
    private Expander RuntimeLogExpander => CameraWorkspaceControl.RuntimeLogExpander;

    private TextBlock UsbCameraSourceStatusText => CameraWorkspaceControl.UsbCameraSourceStatusText;
    private StackPanel UsbCaptureSettingsPanel => CameraWorkspaceControl.UsbCaptureSettingsPanel;
    private TextBlock UsbCaptureModeStatusText => CameraWorkspaceControl.UsbCaptureModeStatusText;
    private StackPanel GridEditorPanel => CameraWorkspaceControl.GridEditorPanel;
    private ComboBox LayerTypeComboBox => CameraWorkspaceControl.LayerTypeComboBox;
    private ListBox CompositionPreviewListBox => CameraWorkspaceControl.CompositionPreviewListBox;
    private TextBox RegionNameTextBox => CameraWorkspaceControl.RegionNameTextBox;
    private TextBox RegionCodeTextBox => CameraWorkspaceControl.RegionCodeTextBox;
    private TextBox RegionCellsTextBox => CameraWorkspaceControl.RegionCellsTextBox;
    private TextBox SampleCountTextBox => CameraWorkspaceControl.SampleCountTextBox;
    private TextBox ThresholdTextBox => CameraWorkspaceControl.ThresholdTextBox;
    private TextBox MotionAreaTextBox => CameraWorkspaceControl.MotionAreaTextBox;
    private TextBox ColorMinPixelsTextBox => CameraWorkspaceControl.ColorMinPixelsTextBox;
    private TextBox MorphKernelSizeTextBox => CameraWorkspaceControl.MorphKernelSizeTextBox;
    private TextBox ProcessWidthTextBox => CameraWorkspaceControl.ProcessWidthTextBox;
    private ComboBox CalibrationColorComboBox => ColorCalibrationWorkspaceControl.CalibrationColorComboBox;
    private ColorPicker CalibrationColorMinPicker => ColorCalibrationWorkspaceControl.CalibrationColorMinPicker;
    private ColorPicker CalibrationColorMaxPicker => ColorCalibrationWorkspaceControl.CalibrationColorMaxPicker;
    private TextBox BakeImagePathTextBox => CameraWorkspaceControl.BakeImagePathTextBox;

    private Control LayersWorkspacePanel => LayerWorkspaceControl;
    private Control ColorCalibrationWorkspacePanel => ColorCalibrationWorkspaceControl;
    private Control SettingsWorkspacePanel => SettingsWorkspaceControl;
    private Control CameraWorkspacePanel => CameraWorkspaceControl;
}
