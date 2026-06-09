using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using FluentAvalonia.UI.Windowing;
using ObjectTracker.Core.Domain;
using OpenCvSharp;
using VideoCapture = OpenCvSharp.VideoCapture;
using VideoCaptureAPIs = OpenCvSharp.VideoCaptureAPIs;
using VideoCaptureProperties = OpenCvSharp.VideoCaptureProperties;

namespace ObjectTracker.UI.Desktop;

public partial class MainWindow : AppWindow
{
    public enum Workspace
    {
        Camera,
        Regions,
        Settings
    }

    public readonly record struct WorkspaceVisibility(bool CameraVisible, bool RegionsVisible, bool SettingsVisible);

    public readonly record struct BottomStatusSnapshot(
        string VisionPipeline,
        string Calibration,
        string PendingRestart);

    public readonly record struct VisionPipelineMenuState(bool StartEnabled, bool StopEnabled);

    public readonly record struct CameraDestructiveActionsState(
        bool DeleteSelectedEnabled,
        bool ClearAllEnabled,
        bool DeleteSelectedRequiresConfirmation,
        bool ClearAllRequiresConfirmation);

    public readonly record struct LayerTypeUsageItem(
        string CameraDisplayName,
        string CameraZoneName,
        string LayerName,
        int RegionCount);

    public readonly record struct LayerTypeUsageProjection(IReadOnlyList<LayerTypeUsageItem> Items);

    public readonly record struct LayerTypeDeleteState(bool CanDelete, string Message);

    public readonly record struct SettingsDraftState(
        AppSettings SavedSettings,
        AppSettings DraftSettings,
        bool HasUnsavedChanges,
        string StatusText);

    public readonly record struct SettingsNavigationResult(
        Workspace Workspace,
        AppSettings SavedSettings,
        AppSettings DraftSettings,
        bool HasUnsavedChanges,
        bool ShouldPersist);

    public readonly record struct SettingsSaveImpact(
        bool RequiresVisionPipelineRestart,
        bool HasPendingVisionPipelineRestart,
        string SettingsStatusText);

    public readonly record struct CameraWorkspaceCamera(
        string CameraId,
        string DisplayName,
        bool IsVisible,
        bool IsIncludedInVisionPipeline,
        bool DebugViewEnabled);

    public readonly record struct CameraWorkspaceTile(string CameraId, string DisplayName, int Index, CameraRenderMode RenderMode);

    public enum CameraRenderMode
    {
        LiveAnnotated,
        DebugView,
        RawFeed
    }

    public enum SettingsNavigationDecision
    {
        Save,
        Discard,
        Cancel
    }

    public enum SettingsField
    {
        GridColumns,
        GridRows
    }

    public readonly record struct CameraGridProjection(
        int VisibleCount,
        int Rows,
        int Columns,
        IReadOnlyList<CameraWorkspaceTile> Tiles);

    public readonly record struct CameraTileViewState(
        int Rows,
        int Columns,
        IReadOnlyList<string> Titles,
        IReadOnlyList<string> CameraIds,
        IReadOnlyList<CameraRenderMode> RenderModes);

    public readonly record struct CameraPanelLayoutState(
        bool IsOpen,
        bool IsPinned,
        SplitViewDisplayMode DisplayMode,
        double CameraPanelWidth,
        double CompactPaneWidth,
        string ToggleButtonText,
        string PinButtonText);

    internal static IReadOnlyList<CameraProfile> GetIncludedCameraProfiles(IReadOnlyList<CameraProfile> cameras)
    {
        var included = new List<CameraProfile>();
        foreach (var camera in cameras)
        {
            if (camera.IsIncludedInVisionPipeline)
            {
                included.Add(camera);
            }
        }

        return included;
    }

    public static WorkspaceVisibility BuildWorkspaceVisibility(Workspace workspace)
    {
        return workspace switch
        {
            Workspace.Camera => new WorkspaceVisibility(true, false, false),
            Workspace.Regions => new WorkspaceVisibility(false, true, false),
            Workspace.Settings => new WorkspaceVisibility(false, false, true),
            _ => new WorkspaceVisibility(true, false, false)
        };
    }

    public static bool IsRuntimeLogVisibleForWorkspace(Workspace workspace)
    {
        return workspace == Workspace.Camera;
    }

    public static VisionPipelineMenuState BuildVisionPipelineMenuState(bool isVisionPipelineRunning)
    {
        return isVisionPipelineRunning
            ? new VisionPipelineMenuState(StartEnabled: false, StopEnabled: true)
            : new VisionPipelineMenuState(StartEnabled: true, StopEnabled: false);
    }

    public static CameraPanelLayoutState BuildCameraPanelLayoutState(bool isOpen, bool isPinned)
    {
        if (isPinned)
        {
            return new CameraPanelLayoutState(
                IsOpen: true,
                IsPinned: true,
                DisplayMode: SplitViewDisplayMode.Inline,
                CameraPanelWidth: 340,
                CompactPaneWidth: 0,
                ToggleButtonText: string.Empty,
                PinButtonText: "📍");
        }

        return new CameraPanelLayoutState(
            IsOpen: isOpen,
            IsPinned: false,
            DisplayMode: SplitViewDisplayMode.CompactOverlay,
            CameraPanelWidth: 340,
            CompactPaneWidth: 48,
            ToggleButtonText: isOpen ? "<" : ">",
            PinButtonText: "📌");
    }

    public static CameraGridProjection BuildCameraGridProjection(IReadOnlyList<CameraWorkspaceCamera> cameras)
    {
        var visible = cameras.Where(camera => camera.IsVisible).ToList();
        var (rows, columns) = ComputeCameraGridDimensions(visible.Count);
        var tiles = visible
            .Select((camera, index) => new CameraWorkspaceTile(
                camera.CameraId,
                camera.DisplayName,
                index,
                GetCameraRenderMode(camera.IsIncludedInVisionPipeline, camera.DebugViewEnabled)))
            .ToList();

        return new CameraGridProjection(visible.Count, rows, columns, tiles);
    }

    public static CameraRenderMode GetCameraRenderMode(bool isIncludedInVisionPipeline, bool debugViewEnabled)
    {
        if (!isIncludedInVisionPipeline)
        {
            return CameraRenderMode.RawFeed;
        }

        return debugViewEnabled ? CameraRenderMode.DebugView : CameraRenderMode.LiveAnnotated;
    }

    public static bool NormalizeDebugViewEnabled(bool isIncludedInVisionPipeline, bool debugViewEnabled)
    {
        return isIncludedInVisionPipeline && debugViewEnabled;
    }

    public static string GetCameraRenderModeBadge(CameraRenderMode mode)
    {
        return mode switch
        {
            CameraRenderMode.RawFeed => "RAW FEED",
            CameraRenderMode.DebugView => "DEBUG VIEW",
            _ => "LIVE ANNOTATED"
        };
    }

    public static (int Rows, int Columns) ComputeCameraGridDimensions(int visibleCount)
    {
        return visibleCount switch
        {
            <= 0 => (0, 0),
            1 => (1, 1),
            2 => (1, 2),
            <= 4 => (2, 2),
            _ => (2, (int)Math.Ceiling(visibleCount / 2d))
        };
    }

    public static CameraTileViewState BuildCameraTileViewState(CameraGridProjection projection)
    {
        var titles = projection.Tiles
            .Select((tile, index) => $"{index + 1}. {tile.DisplayName} [{tile.RenderMode}]")
            .ToList();
        var ids = projection.Tiles.Select(tile => tile.CameraId).ToList();
        var modes = projection.Tiles.Select(tile => tile.RenderMode).ToList();
        return new CameraTileViewState(projection.Rows, projection.Columns, titles, ids, modes);
    }

    public static SelectionMode GetCameraListSelectionMode()
    {
        return SelectionMode.Single;
    }

    public static CameraDestructiveActionsState BuildCameraDestructiveActionsState(
        bool isVisionPipelineRunning,
        bool hasSelectedCamera,
        int cameraCount)
    {
        var runtimeBlocked = isVisionPipelineRunning;
        return new CameraDestructiveActionsState(
            DeleteSelectedEnabled: hasSelectedCamera && !runtimeBlocked,
            ClearAllEnabled: cameraCount > 0 && !runtimeBlocked,
            DeleteSelectedRequiresConfirmation: true,
            ClearAllRequiresConfirmation: true);
    }

    public static string BuildDeleteCameraConfirmationMessage(string cameraDisplayName)
    {
        return $"Delete camera '{cameraDisplayName}' from this Session?";
    }

    public static string BuildClearCamerasConfirmationMessage(int cameraCount)
    {
        return $"Clear all {cameraCount} cameras from this Session? This cannot be undone.";
    }


    public static SettingsDraftState BuildSettingsDraftState(AppSettings savedSettings, AppSettings draftSettings)
    {
        var hasUnsavedChanges = savedSettings != draftSettings;
        return new SettingsDraftState(
            savedSettings,
            draftSettings,
            hasUnsavedChanges,
            hasUnsavedChanges ? "Settings: unsaved changes" : "Settings: saved");
    }

    public static SettingsNavigationResult ApplySettingsNavigationDecision(
        Workspace currentWorkspace,
        Workspace targetWorkspace,
        AppSettings savedSettings,
        AppSettings draftSettings,
        SettingsNavigationDecision decision)
    {
        if (currentWorkspace != Workspace.Settings || savedSettings == draftSettings)
        {
            return new SettingsNavigationResult(targetWorkspace, savedSettings, draftSettings, false, false);
        }

        return decision switch
        {
            SettingsNavigationDecision.Save => new SettingsNavigationResult(targetWorkspace, draftSettings, draftSettings, false, true),
            SettingsNavigationDecision.Discard => new SettingsNavigationResult(targetWorkspace, savedSettings, savedSettings, false, false),
            _ => new SettingsNavigationResult(currentWorkspace, savedSettings, draftSettings, true, false)
        };
    }

    public static string GetSettingsApplyPolicyLabel(SettingsField field)
    {
        return field switch
        {
            SettingsField.GridColumns => "requires Vision Pipeline restart",
            SettingsField.GridRows => "requires Vision Pipeline restart",
            _ => "applies immediately"
        };
    }

    public static SettingsSaveImpact BuildSettingsSaveImpact(AppSettings savedSettings, AppSettings draftSettings, bool isVisionPipelineRunning)
    {
        var requiresRestart = savedSettings.GridColumns != draftSettings.GridColumns
            || savedSettings.GridRows != draftSettings.GridRows;
        var pendingRestart = requiresRestart && isVisionPipelineRunning;
        return new SettingsSaveImpact(
            requiresRestart,
            pendingRestart,
            pendingRestart ? "Settings: saved, pending Vision Pipeline restart" : "Settings: saved");
    }

    public static BottomStatusSnapshot BuildBottomStatusSnapshot(bool isVisionPipelineRunning, bool hasPendingVisionPipelineRestart)
    {
        return new BottomStatusSnapshot(
            VisionPipeline: isVisionPipelineRunning ? "Vision Pipeline: running" : "Vision Pipeline: stopped",
            Calibration: "Calibration: unknown",
            PendingRestart: hasPendingVisionPipelineRestart ? "Pending restart: required" : "Pending restart: none");
    }

    private const int MaxLogEntries = 300;
    private const int PreviewIntervalMs = 33;
    private const int MaxUsbCameraProbeIndex = 5;

    private readonly Lock cameraSync = new();
    private readonly Lock settingsSync = new();
    private readonly List<CameraProfile> cameras = new();
    private readonly Dictionary<string, RuntimeProcessingSettings> cameraSettings = new(StringComparer.OrdinalIgnoreCase);
    private readonly ObservableCollection<string> logEntries = new();

    private readonly BackgroundEstimationEngine engine = new();
    private readonly CameraSettingsStore cameraSettingsStore = new();
    private readonly UsbCaptureSettingsStore usbCaptureSettingsStore = new();
    private readonly CameraZoneBindingStore cameraZoneBindingStore = new();
    private readonly AppSettingsStore appSettingsStore = new();
    private readonly UsbCameraOwnerManager usbCameraOwnerManager = new(new OpenCvUsbCaptureBackend());
    private readonly UsbCaptureSettingsService usbCaptureSettingsService;
    private readonly SessionAuditLogger sessionAuditLogger = new();
    private readonly CameraZoneIdentityService cameraZoneIdentityService;
    private ObjectTracker.UI.Desktop.Region.Contracts.IRegionManagerService regionManagerService;
    private ObjectTracker.UI.Desktop.Region.Contracts.IRegionRegistry regionRegistry;
    private AppSettings appSettings;
    private AppSettings draftAppSettings;

    private CancellationTokenSource? runCts;
    private readonly CameraTileFeedCoordinator cameraTileFeedCoordinator;
    private readonly Dictionary<string, CameraProfile> cameraTileFeedCamerasById = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Image> cameraTileImagesById = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, CameraRenderMode> cameraTileRenderModesById = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DebugTileImageSet> cameraTileDebugImagesById = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> pendingUsbCaptureSettingsCameraSourceIds = new(StringComparer.OrdinalIgnoreCase);
    private Task? runTask;
    private int selectedCameraIndex = -1;
    private int requestedCameraIndex = -1;
    private string selectedCalibrationColor = "red";
    private bool applyingCameraZoneUi;
    private bool applyingCameraVisibilityUi;
    private bool applyingCameraInclusionUi;
    private bool applyingCameraDebugViewUi;
    private bool applyingUsbCaptureSettingsUi;
    private bool hasPendingVisionPipelineRestart;
    private bool isCameraPanelOpen = true;
    private bool isCameraPanelPinned = true;
    private Workspace activeWorkspace = Workspace.Camera;
    internal Func<string, string, Task<bool>> ConfirmDestructiveActionAsync { get; set; }
    internal Func<Task<SettingsNavigationDecision>> PromptSettingsNavigationDecisionAsync { get; set; }

    private sealed record DebugTileImageSet(Image Background, Image Moving, Image Color, Image Motion);

    public MainWindow()
    {
        InitializeComponent();

        if (OperatingSystem.IsWindows())
        {
            TitleBar.ExtendsContentIntoTitleBar = true;
            TitleBar.Height = 40;
        }

        LogListBox.ItemsSource = logEntries;

        foreach (var (cameraId, settings) in cameraSettingsStore.Load())
        {
            cameraSettings[cameraId] = settings;
        }

        usbCaptureSettingsService = new UsbCaptureSettingsService(usbCaptureSettingsStore.Load());

        var cameraZoneSnapshot = cameraZoneBindingStore.Load();
        cameraZoneIdentityService = new CameraZoneIdentityService(cameraZoneSnapshot.Zones, cameraZoneSnapshot.Bindings);
        InitializeRegionServices();
        appSettings = appSettingsStore.Load();
        draftAppSettings = appSettings;
        PromptSettingsNavigationDecisionAsync = ShowSettingsNavigationGuardDialogAsync;
        ConfirmDestructiveActionAsync = ShowDestructiveConfirmationDialogAsync;
        cameraTileFeedCoordinator = new CameraTileFeedCoordinator(StartCameraTileFeedConsumer);
        InitializeUsbCaptureSettingsUi();

        HookEvents();
        SetActiveWorkspace(Workspace.Camera);
        SetRunState(isRunning: false);
        ApplyCameraPanelLayout();
        RefreshSettingsWorkspaceUi();
        RefreshCameraUi();
        AppendLog("Application initialized.");
        UpdateBottomStatusBar();
    }

    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        await StopCameraTilePreviewAsync();
        await StopProcessingAsync();
        await usbCameraOwnerManager.StopAllAsync(CancellationToken.None);
        PersistCameraSettings();
        sessionAuditLogger.Dispose();
        base.OnClosing(e);
    }

    private void HookEvents()
    {
        AddVideosButton.Click += AddCamerasButtonOnClick;
        RemoveSelectedButton.Click += RemoveCameraButtonOnClick;
        DeleteSelectedCameraMenuItem.Click += RemoveCameraButtonOnClick;
        MoveCameraUpButton.Click += MoveCameraUpButtonOnClick;
        MoveCameraDownButton.Click += MoveCameraDownButtonOnClick;
        ClearCamerasMenuItem.Click += ClearCamerasButtonOnClick;
        PreviousVideoButton.Click += PreviousCameraButtonOnClick;
        NextVideoButton.Click += NextCameraButtonOnClick;
        StartStopButton.Click += StartStopButtonOnClick;
        OpenBakedMaskButton.Click += OpenBakedMaskButtonOnClick;
        PlaylistListBox.SelectionChanged += CameraSelectionChanged;
        CameraVisibilityCheckBox.IsCheckedChanged += CameraVisibilityCheckBoxOnChanged;
        VisionPipelineInclusionCheckBox.IsCheckedChanged += VisionPipelineInclusionCheckBoxOnChanged;
        CameraDebugViewCheckBox.IsCheckedChanged += CameraDebugViewCheckBoxOnChanged;
        RestartUsbCameraSourceButton.Click += RestartUsbCameraSourceButtonOnClick;
        UsbResolutionComboBox.SelectionChanged += UsbCaptureSettingsControlOnChanged;
        UsbTargetFpsComboBox.SelectionChanged += UsbCaptureSettingsControlOnChanged;
        ApplyUsbCaptureSettingsButton.Click += ApplyUsbCaptureSettingsButtonOnClick;
        RevertUsbCaptureSettingsButton.Click += RevertUsbCaptureSettingsButtonOnClick;
        BakeSourceComboBox.SelectionChanged += BakeSourceComboBoxOnSelectionChanged;
        SelectBakeImageButton.Click += SelectBakeImageButtonOnClick;
        ClearBakeImageButton.Click += ClearBakeImageButtonOnClick;
        OpenCameraWorkspaceMenuItem.Click += CameraWorkspaceButtonOnClick;
        RegionsWorkspaceMenuItem.Click += RegionsWorkspaceButtonOnClick;
        SettingsWorkspaceMenuItem.Click += SettingsWorkspaceButtonOnClick;
        CreateRegionButton.Click += CreateRegionButtonOnClick;
        EditRegionButton.Click += EditRegionButtonOnClick;
        DeleteRegionButton.Click += DeleteRegionButtonOnClick;
        RegionsListBox.SelectionChanged += RegionsListBoxOnSelectionChanged;
        StartVisionPipelineMenuItem.Click += StartVisionPipelineMenuItemOnClick;
        StopVisionPipelineMenuItem.Click += StopVisionPipelineMenuItemOnClick;
        ToggleCameraPanelButton.Click += ToggleCameraPanelButtonOnClick;
        PinCameraPanelButton.Click += PinCameraPanelButtonOnClick;
        SaveSettingsButton.Click += SaveSettingsButtonOnClick;
        DiscardSettingsButton.Click += DiscardSettingsButtonOnClick;
        SettingsGridColumnsTextBox.TextChanged += SettingsDraftTextBoxOnTextChanged;
        SettingsGridRowsTextBox.TextChanged += SettingsDraftTextBoxOnTextChanged;

        SampleCountTextBox.LostFocus += RuntimeSettingControlOnLostFocus;
        ThresholdTextBox.LostFocus += RuntimeSettingControlOnLostFocus;
        MotionAreaTextBox.LostFocus += RuntimeSettingControlOnLostFocus;
        ColorMinPixelsTextBox.LostFocus += RuntimeSettingControlOnLostFocus;
        MorphKernelSizeTextBox.LostFocus += RuntimeSettingControlOnLostFocus;
        ProcessWidthTextBox.LostFocus += RuntimeSettingControlOnLostFocus;

        CalibrationColorComboBox.SelectionChanged += CalibrationColorComboBoxOnSelectionChanged;
        HueLowerTextBox.LostFocus += ColorCalibrationControlOnLostFocus;
        HueUpperTextBox.LostFocus += ColorCalibrationControlOnLostFocus;
        SaturationLowerTextBox.LostFocus += ColorCalibrationControlOnLostFocus;
        SaturationUpperTextBox.LostFocus += ColorCalibrationControlOnLostFocus;
        ValueLowerTextBox.LostFocus += ColorCalibrationControlOnLostFocus;
        ValueUpperTextBox.LostFocus += ColorCalibrationControlOnLostFocus;

    }

    private async void CameraWorkspaceButtonOnClick(object? sender, RoutedEventArgs e)
    {
        await TryNavigateWorkspaceAsync(Workspace.Camera);
    }

    private async void LayersWorkspaceButtonOnClick(object? sender, RoutedEventArgs e)
    {
        await TryNavigateWorkspaceAsync(Workspace.Regions);
    }

    private async void SettingsWorkspaceButtonOnClick(object? sender, RoutedEventArgs e)
    {
        await TryNavigateWorkspaceAsync(Workspace.Settings);
    }

    private async void StartVisionPipelineMenuItemOnClick(object? sender, RoutedEventArgs e)
    {
        if (runTask is null)
        {
            await StartVisionPipelineFromUiAsync();
        }
    }

    private void ToggleCameraPanelButtonOnClick(object? sender, RoutedEventArgs e)
    {
        if (isCameraPanelPinned)
        {
            return;
        }

        isCameraPanelOpen = !isCameraPanelOpen;
        ApplyCameraPanelLayout();
    }

    private void PinCameraPanelButtonOnClick(object? sender, RoutedEventArgs e)
    {
        isCameraPanelPinned = !isCameraPanelPinned;
        isCameraPanelOpen = true;
        ApplyCameraPanelLayout();
    }

    private void ApplyCameraPanelLayout()
    {
        var layout = BuildCameraPanelLayoutState(isCameraPanelOpen, isCameraPanelPinned);

        CameraSplitView.CompactPaneLength = layout.CompactPaneWidth;
        CameraSplitView.IsPaneOpen = layout.IsOpen;
        CameraSplitView.DisplayMode = layout.DisplayMode;
        CameraSplitView.OpenPaneLength = layout.CameraPanelWidth;
        CameraPanelFullContent.IsVisible = layout.IsOpen;
        CameraPanelTitleText.IsVisible = layout.IsOpen;
        PinCameraPanelButton.IsVisible = layout.IsOpen;
        ToggleCameraPanelButton.IsVisible = !layout.IsPinned;

        ToggleCameraPanelButton.Content = layout.ToggleButtonText;
        PinCameraPanelButton.Content = layout.PinButtonText;
    }

    private async void StopVisionPipelineMenuItemOnClick(object? sender, RoutedEventArgs e)
    {
        if (runTask is not null)
        {
            await StopProcessingAsync();
        }
    }

    public void SetActiveWorkspace(Workspace workspace)
    {
        activeWorkspace = workspace;
        var visibility = BuildWorkspaceVisibility(workspace);
        CameraWorkspacePanel.IsVisible = visibility.CameraVisible;
        RegionsWorkspacePanel.IsVisible = visibility.RegionsVisible;
        SettingsWorkspacePanel.IsVisible = visibility.SettingsVisible;
        RuntimeLogExpander.IsVisible = IsRuntimeLogVisibleForWorkspace(workspace);
    }

    public Workspace GetActiveWorkspace()
    {
        return activeWorkspace;
    }

    private async Task TryNavigateWorkspaceAsync(Workspace targetWorkspace)
    {
        if (targetWorkspace == activeWorkspace)
        {
            return;
        }

        var draftState = BuildSettingsDraftState(appSettings, draftAppSettings);
        var decision = activeWorkspace == Workspace.Settings && draftState.HasUnsavedChanges
            ? await PromptSettingsNavigationDecisionAsync()
            : SettingsNavigationDecision.Discard;
        var result = ApplySettingsNavigationDecision(activeWorkspace, targetWorkspace, appSettings, draftAppSettings, decision);
        var impact = result.ShouldPersist
            ? BuildSettingsSaveImpact(appSettings, draftAppSettings, runTask is not null)
            : new SettingsSaveImpact(false, false, "Settings: saved");

        appSettings = result.SavedSettings;
        draftAppSettings = result.DraftSettings;
        if (result.ShouldPersist)
        {
            hasPendingVisionPipelineRestart = hasPendingVisionPipelineRestart || impact.HasPendingVisionPipelineRestart;
            appSettingsStore.Save(appSettings);
        }

        RefreshSettingsWorkspaceUi();
        UpdateBottomStatusBar();
        SetActiveWorkspace(result.Workspace);
    }

    private void SaveSettingsButtonOnClick(object? sender, RoutedEventArgs e)
    {
        UpdateDraftAppSettingsFromUi();
        var impact = BuildSettingsSaveImpact(appSettings, draftAppSettings, runTask is not null);
        appSettings = draftAppSettings;
        hasPendingVisionPipelineRestart = hasPendingVisionPipelineRestart || impact.HasPendingVisionPipelineRestart;
        appSettingsStore.Save(appSettings);
        RefreshSettingsWorkspaceUi();
        UpdateBottomStatusBar();
        SetStatus("Status: settings saved.");
    }

    private void DiscardSettingsButtonOnClick(object? sender, RoutedEventArgs e)
    {
        draftAppSettings = appSettings;
        RefreshSettingsWorkspaceUi();
        SetStatus("Status: settings changes discarded.");
    }

    private void SettingsDraftTextBoxOnTextChanged(object? sender, TextChangedEventArgs e)
    {
        UpdateDraftAppSettingsFromUi();
        RefreshSettingsDraftStatusUi();
    }

    private void UpdateDraftAppSettingsFromUi()
    {
        var columns = ParseInt(SettingsGridColumnsTextBox.Text, appSettings.GridColumns, AppSettings.MinGridColumns, AppSettings.MaxGridColumns);
        var rows = ParseInt(SettingsGridRowsTextBox.Text, appSettings.GridRows, AppSettings.MinGridRows, AppSettings.MaxGridRows);
        draftAppSettings = new AppSettings(columns, rows);
    }

    private void RefreshSettingsWorkspaceUi()
    {
        SettingsGridColumnsPolicyText.Text = GetSettingsApplyPolicyLabel(SettingsField.GridColumns);
        SettingsGridRowsPolicyText.Text = GetSettingsApplyPolicyLabel(SettingsField.GridRows);
        SettingsGridColumnsTextBox.Text = draftAppSettings.GridColumns.ToString();
        SettingsGridRowsTextBox.Text = draftAppSettings.GridRows.ToString();
        RefreshSettingsDraftStatusUi();
    }

    private void RefreshSettingsDraftStatusUi()
    {
        var state = BuildSettingsDraftState(appSettings, draftAppSettings);
        SettingsDraftStatusText.Text = state.StatusText;
        SettingsPendingRestartText.Text = hasPendingVisionPipelineRestart
            ? "Pending restart: required"
            : "Pending restart: none";
        SaveSettingsButton.IsEnabled = state.HasUnsavedChanges;
        DiscardSettingsButton.IsEnabled = state.HasUnsavedChanges;
    }

    private async void AddCamerasButtonOnClick(object? sender, RoutedEventArgs e)
    {
        var selection = await new CameraSourceChoiceDialog().ShowDialog<CameraAddChoice?>(this);
        if (selection is null)
        {
            return;
        }

        switch (selection.Value)
        {
            case CameraAddChoice.VideoFiles:
                await AddVideoCamerasAsync();
                break;
            case CameraAddChoice.UsbCamera:
                await AddUsbCameraAsync();
                break;
        }
    }

    private async Task AddVideoCamerasAsync()
    {
        if (StorageProvider is null)
        {
            SetStatus("Status: file picker is not available in this runtime.");
            return;
        }

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select camera video files",
            AllowMultiple = true,
            FileTypeFilter = new List<FilePickerFileType>
            {
                new ("Video files") { Patterns = new[] { "*.mp4", "*.avi", "*.mov", "*.mkv", "*.wmv", "*.m4v" } }
            }
        });

        var added = 0;
        lock (cameraSync)
        {
            foreach (var file in files)
            {
                var path = file.TryGetLocalPath();
                if (string.IsNullOrWhiteSpace(path) || cameras.Any(c => string.Equals(c.Id, path, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                var cameraId = path;
                var displayName = BuildCameraName(path, cameras.Count + 1);
                cameras.Add(CameraProfile.CreateVideo(cameraId, displayName, new List<string> { path }));
                cameraZoneIdentityService.AssignSourceToZone(cameraId, requestedZoneName: displayName);

                if (!cameraSettings.ContainsKey(cameraId))
                {
                    cameraSettings[cameraId] = RuntimeProcessingSettings.Default;
                }

                added++;
            }

            if (selectedCameraIndex < 0 && cameras.Count > 0)
            {
                selectedCameraIndex = 0;
            }
        }

        PersistCameraSettings();
        PersistCameraZones();
        RefreshCameraUi();
        SetStatus(added == 0
            ? "Status: no new video cameras added."
            : $"Status: added {added} video camera(s).");

        if (runTask is not null)
        {
            StartBakeForAllCameras(runCts?.Token ?? CancellationToken.None);
        }
    }

    private async Task AddUsbCameraAsync()
    {
        SetStatus("Status: scanning USB cameras...");
        var alreadyAddedSourceIds = GetAddedUsbCameraSourceIds();
        var usbOptions = await Task.Run(() => DiscoverUsbCameraOptions(alreadyAddedSourceIds));
        if (usbOptions.Count == 0)
        {
            SetStatus("Status: no USB cameras detected.");
            return;
        }

        var selectedOption = await new UsbCameraSelectionDialog(usbOptions).ShowDialog<UsbCameraOption?>(this);
        if (selectedOption is null)
        {
            SetStatus("Status: USB camera selection cancelled.");
            return;
        }

        var option = selectedOption.Value;
        var added = false;

        lock (cameraSync)
        {
            if (!cameras.Any(camera => string.Equals(camera.Id, option.Id, StringComparison.OrdinalIgnoreCase)))
            {
                cameras.Add(CameraProfile.CreateUsb(option.Id, option.DisplayName, option.CameraIndex, option.Api));
                cameraZoneIdentityService.AssignSourceToZone(option.Id, requestedZoneName: option.DisplayName);

                if (!cameraSettings.ContainsKey(option.Id))
                {
                    cameraSettings[option.Id] = RuntimeProcessingSettings.Default;
                }

                if (selectedCameraIndex < 0)
                {
                    selectedCameraIndex = 0;
                }

                added = true;
            }
        }

        if (!added)
        {
            SetStatus($"Status: {option.DisplayName} is already added.");
            return;
        }

        PersistCameraSettings();
        PersistCameraZones();
        RefreshCameraUi();
        SetStatus($"Status: added {option.DisplayName}.");
    }

    private void BakeSourceComboBoxOnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        ApplyBakeSourceUiState();
        UpdateSelectedCameraSettingsFromUi(logChange: false);
    }

    private async void SelectBakeImageButtonOnClick(object? sender, RoutedEventArgs e)
    {
        var camera = GetSelectedCamera();
        if (camera is null)
        {
            SetStatus("Status: select a camera first.");
            return;
        }

        if (StorageProvider is null)
        {
            SetStatus("Status: file picker is not available in this runtime.");
            return;
        }

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select bake image",
            AllowMultiple = false,
            FileTypeFilter = new List<FilePickerFileType>
            {
                new ("Image files") { Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.tif", "*.tiff", "*.webp" } }
            }
        });

        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        BakeSourceComboBox.SelectedIndex = (int)BakeSourceMode.ImageFile;
        BakeImagePathTextBox.Text = path;
        ApplyBakeSourceUiState();
        UpdateSelectedCameraSettingsFromUi(logChange: false);
        SetStatus($"Status: bake image selected for {camera.Value.DisplayName}.");
    }

    private void ClearBakeImageButtonOnClick(object? sender, RoutedEventArgs e)
    {
        var camera = GetSelectedCamera();
        if (camera is null)
        {
            SetStatus("Status: select a camera first.");
            return;
        }

        BakeImagePathTextBox.Text = string.Empty;
        ApplyBakeSourceUiState();
        UpdateSelectedCameraSettingsFromUi(logChange: false);
        SetStatus($"Status: cleared bake image for {camera.Value.DisplayName}.");
    }

    private async void RemoveCameraButtonOnClick(object? sender, RoutedEventArgs e)
    {
        if (!TryResolveSelectedCameraForDestructiveAction(out var cameraIndex, out var selected))
        {
            return;
        }

        var cameraDisplayName = selected.DisplayName;
        var confirmed = await ConfirmDestructiveActionAsync(
            "Delete Camera",
            BuildDeleteCameraConfirmationMessage(cameraDisplayName));
        if (!confirmed)
        {
            SetStatus($"Status: delete canceled for {cameraDisplayName}.");
            return;
        }

        CameraProfile? removed = null;

        lock (cameraSync)
        {
            if (cameraIndex < 0 || cameraIndex >= cameras.Count)
            {
                return;
            }

            removed = cameras[cameraIndex];
            cameras.RemoveAt(cameraIndex);
            cameraSettings.Remove(removed.Value.Id);
            cameraZoneIdentityService.RemoveSourceBinding(removed.Value.Id);

            if (cameras.Count == 0)
            {
                selectedCameraIndex = -1;
            }
            else
            {
                selectedCameraIndex = Math.Clamp(cameraIndex, 0, cameras.Count - 1);
            }
        }

        PersistCameraSettings();
        PersistCameraZones();
        RefreshCameraUi();

        if (runTask is not null && selectedCameraIndex >= 0)
        {
            Interlocked.Exchange(ref requestedCameraIndex, selectedCameraIndex);
        }

        SetStatus($"Status: removed camera {removed?.DisplayName ?? "-"}.");
    }

    private async void ClearCamerasButtonOnClick(object? sender, RoutedEventArgs e)
    {
        var count = GetCameraCount();
        if (count <= 0)
        {
            return;
        }

        if (runTask is not null)
        {
            SetStatus("Status: stop Vision Pipeline before clearing cameras.");
            return;
        }

        var confirmed = await ConfirmDestructiveActionAsync(
            "Clear Cameras",
            BuildClearCamerasConfirmationMessage(count));
        if (!confirmed)
        {
            SetStatus("Status: clear cameras canceled.");
            return;
        }

        lock (cameraSync)
        {
            cameras.Clear();
            cameraSettings.Clear();
            selectedCameraIndex = -1;
        }

        await StopCameraTilePreviewAsync();
        await usbCameraOwnerManager.StopAllAsync(CancellationToken.None);

        PersistCameraSettings();
        PersistCameraZones();
        RefreshCameraUi();
        SetStatus("Status: all cameras cleared.");
    }

    private void PreviousCameraButtonOnClick(object? sender, RoutedEventArgs e)
    {
        NavigateCameraRelative(-1);
    }

    private void NextCameraButtonOnClick(object? sender, RoutedEventArgs e)
    {
        NavigateCameraRelative(1);
    }

    private void CameraSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var index = PlaylistListBox.SelectedIndex;
        CameraProfile? camera = null;

        lock (cameraSync)
        {
            if (index < 0 || index >= cameras.Count)
            {
                return;
            }

            selectedCameraIndex = index;
            camera = cameras[index];
        }

        if (camera is not null)
        {
            ApplySettingsToUi(GetSettingsForCamera(camera.Value.Id));
            CurrentVideoText.Text = BuildCurrentSourceText(camera.Value);
            OpenBakedMaskButton.IsEnabled = camera.Value.CanOpenBakedMask;
            applyingCameraVisibilityUi = true;
            CameraVisibilityCheckBox.IsChecked = camera.Value.IsVisible;
            applyingCameraVisibilityUi = false;
            applyingCameraInclusionUi = true;
            VisionPipelineInclusionCheckBox.IsChecked = camera.Value.IsIncludedInVisionPipeline;
            applyingCameraInclusionUi = false;
            applyingCameraDebugViewUi = true;
            CameraDebugViewCheckBox.IsChecked = NormalizeDebugViewEnabled(camera.Value.IsIncludedInVisionPipeline, camera.Value.DebugViewEnabled);
            applyingCameraDebugViewUi = false;
            CameraDebugViewCheckBox.IsEnabled = camera.Value.IsIncludedInVisionPipeline;
            RefreshSelectedUsbCameraSourceStatusUi(camera.Value);
            RefreshUsbCaptureSettingsUi(camera.Value);
        }

        if (runTask is not null && index >= 0)
        {
            Interlocked.Exchange(ref requestedCameraIndex, index);
            SetStatus($"Status: switching to camera {camera?.DisplayName}...");
            if (camera is { } selected)
            {
                sessionAuditLogger.AppendEvent(
                    SessionAuditLogger.EventCameraSwitch,
                    "Camera switch requested from playlist selection.",
                    ("cameraId", selected.Id),
                    ("cameraName", selected.DisplayName),
                    ("requestedIndex", index.ToString()));
            }
        }
    }

    private async void StartStopButtonOnClick(object? sender, RoutedEventArgs e)
    {
        if (runTask is not null)
        {
            await StopProcessingAsync();
            return;
        }

        await StartVisionPipelineFromUiAsync();
    }

    private void MoveCameraUpButtonOnClick(object? sender, RoutedEventArgs e)
    {
        MoveSelectedCameraBy(-1);
    }

    private void MoveCameraDownButtonOnClick(object? sender, RoutedEventArgs e)
    {
        MoveSelectedCameraBy(1);
    }

    private void MoveSelectedCameraBy(int delta)
    {
        lock (cameraSync)
        {
            if (selectedCameraIndex < 0 || selectedCameraIndex >= cameras.Count)
            {
                return;
            }

            var target = selectedCameraIndex + delta;
            if (target < 0 || target >= cameras.Count)
            {
                return;
            }

            var selected = cameras[selectedCameraIndex];
            cameras.RemoveAt(selectedCameraIndex);
            cameras.Insert(target, selected);
            selectedCameraIndex = target;
        }

        RefreshCameraUi();
    }

    private async Task StartVisionPipelineFromUiAsync()
    {
        if (runTask is not null)
        {
            return;
        }

        if (GetCameraCount() == 0)
        {
            SetStatus("Status: add at least one camera.");
            return;
        }

        UpdateSelectedCameraSettingsFromUi(logChange: false);

        var loopCameraVideos = LoopPlaylistCheckBox.IsChecked == true;
        var runStopwatch = Stopwatch.StartNew();
        var stopReason = "completed";

        runCts = new CancellationTokenSource();
        var token = runCts.Token;
        sessionAuditLogger.StartSession();

        List<CameraProfile> snapshot;
        lock (cameraSync)
        {
            snapshot = cameras.ToList();
        }

        var includedCameras = GetIncludedCameraProfiles(snapshot);
        sessionAuditLogger.AppendEvent(
            SessionAuditLogger.EventRunStart,
            "Processing run started.",
            ("cameraCount", GetCameraCount().ToString()),
            ("includedCameraCount", includedCameras.Count.ToString()),
            ("loopVideos", loopCameraVideos.ToString()));

        if (!string.IsNullOrWhiteSpace(sessionAuditLogger.CurrentFilePath))
        {
            SetStatus($"Status: session log active at {sessionAuditLogger.CurrentFilePath}");
        }

        hasPendingVisionPipelineRestart = false;
        SetRunState(isRunning: true);
        StartBakeForAllCameras(token);
        runTask = Task.Run(() => RunAllCamerasAsync(includedCameras, loopCameraVideos, token), token);

        try
        {
            await runTask;
        }
        catch (OperationCanceledException)
        {
            stopReason = "canceled";
            SetStatus("Status: processing stopped.");
        }
        catch (Exception ex)
        {
            stopReason = "error";
            SetStatus($"Status: error - {ex.Message}");
            sessionAuditLogger.AppendEvent(
                SessionAuditLogger.EventRunStop,
                "Processing run failed.",
                ("reason", stopReason),
                ("durationMs", runStopwatch.ElapsedMilliseconds.ToString()),
                ("error", ex.Message));
        }
        finally
        {
            runTask = null;
            runCts?.Dispose();
            runCts = null;

            if (stopReason != "error")
            {
                sessionAuditLogger.AppendEvent(
                    SessionAuditLogger.EventRunStop,
                    "Processing run stopped.",
                    ("reason", stopReason),
                    ("durationMs", runStopwatch.ElapsedMilliseconds.ToString()));
            }

            sessionAuditLogger.StopSession();
            SetRunState(isRunning: false);
            await ApplyPendingUsbCaptureSettingsAfterVisionPipelineStopAsync();
        }
    }

    private async void OpenBakedMaskButtonOnClick(object? sender, RoutedEventArgs e)
    {
        var camera = GetSelectedCamera();
        if (camera is null)
        {
            SetStatus("Status: select a camera first.");
            return;
        }

        if (camera.Value.IsUsbCamera)
        {
            SetStatus("Status: baked masks are only available for video cameras.");
            return;
        }

        UpdateSelectedCameraSettingsFromUi(logChange: false);
        var settings = GetSettingsForCamera(camera.Value.Id);
        var options = new BackgroundEstimationEngine.ProcessingOptions(
            settings.ProcessMaxWidth,
            settings.MotionArea,
            settings.ColorMinPixels,
            settings.MorphKernelSize,
            settings.ColorCalibrations);

        string? bakedPath;
        try
        {
            bakedPath = await engine.EnsureBakedBackgroundAsync(
                camera.Value.PrimaryVideoPath,
                settings.SampleCount,
                options,
                GetBakeImagePath(settings),
                CancellationToken.None,
                async message => await Dispatcher.UIThread.InvokeAsync(() => SetStatus($"Status: {message}")));
        }
        catch (Exception ex)
        {
            SetStatus($"Status: failed to prepare baked mask - {ex.Message}");
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = bakedPath,
                UseShellExecute = true
            });

            SetStatus($"Status: opened baked mask {Path.GetFileName(bakedPath)}");
        }
        catch (Exception ex)
        {
            SetStatus($"Status: failed to open baked mask - {ex.Message}");
        }
    }

    private async Task StopProcessingAsync()
    {
        var task = runTask;
        if (task is null)
        {
            return;
        }

        runCts?.Cancel();

        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
            // Ignore cancellation while stopping.
        }
        catch
        {
            // Ignore stop-time exceptions.
        }

        await ApplyPendingUsbCaptureSettingsAfterVisionPipelineStopAsync();
    }

    private async Task ApplyPendingUsbCaptureSettingsAfterVisionPipelineStopAsync()
    {
        if (pendingUsbCaptureSettingsCameraSourceIds.Count == 0)
        {
            return;
        }

        var pending = pendingUsbCaptureSettingsCameraSourceIds.ToList();
        pendingUsbCaptureSettingsCameraSourceIds.Clear();

        foreach (var cameraSourceId in pending)
        {
            if (!TryGetCameraById(cameraSourceId, out var camera) ||
                !camera.IsVisible ||
                camera.UsbCamera is not { } usb)
            {
                continue;
            }

            var key = new UsbCameraKey(usb.CameraIndex, usb.Api.ToString().ToUpperInvariant());
            await usbCameraOwnerManager.RestartAsync(key, ToUsbCaptureSettings(usbCaptureSettingsService.GetRequestedSettings(camera.Id)), CancellationToken.None);
        }

        hasPendingVisionPipelineRestart = false;
        UpdateBottomStatusBar();
    }

    private async Task RunAllCamerasAsync(IReadOnlyList<CameraProfile> cameras, bool loopCameraVideos, CancellationToken cancellationToken)
    {
        var tasks = new List<Task>();

        foreach (var camera in cameras)
        {
            var engine = new BackgroundEstimationEngine();
            var cameraCopy = camera;
            tasks.Add(Task.Run(() => ProcessCameraAsync(cameraCopy, loopCameraVideos, engine, cancellationToken), cancellationToken));
        }

        await Task.WhenAll(tasks);
    }

    private async Task ProcessCameraAsync(CameraProfile camera, bool loopCameraVideos, BackgroundEstimationEngine engine, CancellationToken cancellationToken)
    {
        if (camera.IsUsbCamera && camera.UsbCamera is { } usbCamera)
        {
            var settings = GetSettingsForCamera(camera.Id);
            var options = new BackgroundEstimationEngine.ProcessingOptions(
                settings.ProcessMaxWidth,
                settings.MotionArea,
                settings.ColorMinPixels,
                settings.MorphKernelSize,
                settings.ColorCalibrations);

            var key = new UsbCameraKey(usbCamera.CameraIndex, usbCamera.Api.ToString().ToUpperInvariant());
            var startupSettings = ToUsbCaptureSettings(usbCaptureSettingsService.GetRequestedSettings(camera.Id));
            var result = await engine.ProcessUsbCameraSourceAsync(
                usbCameraOwnerManager,
                key,
                startupSettings,
                camera.DisplayName,
                settings.SampleCount,
                settings.Threshold,
                options,
                GetBakeImagePath(settings),
                onFrame: frameSet =>
                {
                    QueuePreviewFrame(camera.Id, frameSet, cancellationToken);
                    return Task.CompletedTask;
                },
                onStatus: async message => await Dispatcher.UIThread.InvokeAsync(() => SetStatus($"Status: {message}")),
                getLiveTuning: () => GetLiveTuningForCamera(camera.Id),
                shouldStopEarly: null,
                cancellationToken: cancellationToken);

            if (!result.Success)
            {
                await Dispatcher.UIThread.InvokeAsync(() => SetStatus($"Status: {result.Message}"));
            }

            return;
        }

        for (var videoIndex = 0; !cancellationToken.IsCancellationRequested; videoIndex++)
        {
            var settings = GetSettingsForCamera(camera.Id);
            var options = new BackgroundEstimationEngine.ProcessingOptions(
                settings.ProcessMaxWidth,
                settings.MotionArea,
                settings.ColorMinPixels,
                settings.MorphKernelSize,
                settings.ColorCalibrations);

            if (videoIndex >= camera.VideoPaths.Count)
            {
                if (!loopCameraVideos)
                {
                    break;
                }

                videoIndex = 0;
            }

            var videoPath = camera.VideoPaths[videoIndex];
            await Dispatcher.UIThread.InvokeAsync(() => CurrentVideoText.Text = BuildCurrentSourceText(camera, Path.GetFileName(videoPath)));

            var result = await engine.ProcessVideoAsync(
                videoPath,
                settings.SampleCount,
                settings.Threshold,
                options,
                GetBakeImagePath(settings),
                onFrame: frameSet =>
                {
                    QueuePreviewFrame(camera.Id, frameSet, cancellationToken);
                    return Task.CompletedTask;
                },
                onStatus: async message => await Dispatcher.UIThread.InvokeAsync(() => SetStatus($"Status: {message}")),
                getLiveTuning: () => GetLiveTuningForCamera(camera.Id),
                shouldStopEarly: null,
                cancellationToken: cancellationToken);

            if (!result.Success)
            {
                await Dispatcher.UIThread.InvokeAsync(() => SetStatus($"Status: {result.Message}"));
            }
        }
    }

    private void StartBakeForAllCameras(CancellationToken cancellationToken)
    {
        List<CameraProfile> snapshot;
        lock (cameraSync)
        {
            snapshot = cameras.ToList();
        }

        foreach (var camera in snapshot)
        {
            if (camera.IsUsbCamera)
            {
                continue;
            }

            var settings = GetSettingsForCamera(camera.Id);
            var options = new BackgroundEstimationEngine.ProcessingOptions(
                settings.ProcessMaxWidth,
                settings.MotionArea,
                settings.ColorMinPixels,
                settings.MorphKernelSize,
                settings.ColorCalibrations);

            foreach (var videoPath in camera.VideoPaths)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await engine.EnsureBakedBackgroundAsync(
                            videoPath,
                            settings.SampleCount,
                            options,
                            GetBakeImagePath(settings),
                            cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        // Ignore cancellation during stop.
                    }
                    catch
                    {
                        // Ignore bake errors in background threads.
                    }
                }, cancellationToken);
            }
        }
    }

    private void QueuePreviewFrame(string cameraId, BackgroundEstimationEngine.PreviewFrameSet frameSet, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        var now = Environment.TickCount64;

        lock (cameraPreviewThrottleSync)
        {
            if (!cameraPreviewLastTick.TryGetValue(cameraId, out var last))
            {
                last = 0;
            }

            if (now - last < PreviewIntervalMs)
            {
                return;
            }

            if (cameraPreviewBusy.Contains(cameraId))
            {
                return;
            }

            cameraPreviewBusy.Add(cameraId);
            cameraPreviewLastTick[cameraId] = now;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                await Dispatcher.UIThread.InvokeAsync(() => RenderFrameSet(cameraId, frameSet));
            }
            catch
            {
                // Ignore preview-render errors.
            }
            finally
            {
                lock (cameraPreviewThrottleSync)
                {
                    cameraPreviewBusy.Remove(cameraId);
                }
            }
        }, cancellationToken);
    }

    private readonly object cameraPreviewThrottleSync = new();
    private readonly Dictionary<string, long> cameraPreviewLastTick = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> cameraPreviewBusy = new(StringComparer.OrdinalIgnoreCase);

    private void RenderFrameSet(string cameraId, BackgroundEstimationEngine.PreviewFrameSet frameSet)
    {
        if (string.IsNullOrWhiteSpace(cameraId))
        {
            return;
        }

        if (!cameraTileDebugImagesById.TryGetValue(cameraId, out var debugImages))
        {
            return;
        }

        UpdatePreviewImage(debugImages.Background, frameSet.BackgroundMaskJpeg);
        UpdatePreviewImage(debugImages.Moving, frameSet.MovingColorJpeg);
        UpdatePreviewImage(debugImages.Color, frameSet.ColorDetectionJpeg);
        UpdatePreviewImage(debugImages.Motion, frameSet.MotionJpeg);
    }

    private static void UpdatePreviewImage(Image target, byte[] imageBytes)
    {
        using var ms = new MemoryStream(imageBytes);
        var bitmap = new Bitmap(ms);

        var previous = target.Source as Bitmap;
        target.Source = bitmap;
        previous?.Dispose();
    }

    private void RefreshCameraUi()
    {
        List<CameraProfile> snapshot;
        lock (cameraSync)
        {
            snapshot = cameras.ToList();
        }

        PlaylistListBox.ItemsSource = snapshot.ConvertAll(camera => camera.DisplayName);
        RefreshCameraWorkspaceTiles(snapshot);
        ApplyCameraDestructiveActionsState(snapshot.Count);

        var canNavigate = snapshot.Count > 1;
        PreviousVideoButton.IsEnabled = canNavigate;
        NextVideoButton.IsEnabled = canNavigate;
        MoveCameraUpButton.IsEnabled = selectedCameraIndex > 0;
        MoveCameraDownButton.IsEnabled = selectedCameraIndex >= 0 && selectedCameraIndex < snapshot.Count - 1;

        if (snapshot.Count == 0)
        {
            PlaylistListBox.SelectedIndex = -1;
            CurrentVideoText.Text = "Current camera/source: -";
            OpenBakedMaskButton.IsEnabled = false;
            applyingCameraVisibilityUi = true;
            CameraVisibilityCheckBox.IsChecked = false;
            applyingCameraVisibilityUi = false;
            CameraVisibilityCheckBox.IsEnabled = false;
            applyingCameraInclusionUi = true;
            VisionPipelineInclusionCheckBox.IsChecked = false;
            applyingCameraInclusionUi = false;
            VisionPipelineInclusionCheckBox.IsEnabled = false;
            applyingCameraDebugViewUi = true;
            CameraDebugViewCheckBox.IsChecked = false;
            applyingCameraDebugViewUi = false;
            CameraDebugViewCheckBox.IsEnabled = false;
            UsbCameraSourceStatusText.Text = "USB Camera Source: -";
            RestartUsbCameraSourceButton.IsVisible = false;
            RestartUsbCameraSourceButton.IsEnabled = false;
            UsbCaptureSettingsPanel.IsVisible = false;
            RegionsListBox.ItemsSource = null;
            return;
        }

        selectedCameraIndex = Math.Clamp(selectedCameraIndex, 0, snapshot.Count - 1);
        PlaylistListBox.SelectedIndex = selectedCameraIndex;

        var selected = snapshot[selectedCameraIndex];
        ApplySettingsToUi(GetSettingsForCamera(selected.Id));
        CurrentVideoText.Text = BuildCurrentSourceText(selected);
        OpenBakedMaskButton.IsEnabled = selected.CanOpenBakedMask;
        CameraVisibilityCheckBox.IsEnabled = true;
        applyingCameraVisibilityUi = true;
        CameraVisibilityCheckBox.IsChecked = selected.IsVisible;
        applyingCameraVisibilityUi = false;
        VisionPipelineInclusionCheckBox.IsEnabled = true;
        applyingCameraInclusionUi = true;
        VisionPipelineInclusionCheckBox.IsChecked = selected.IsIncludedInVisionPipeline;
        applyingCameraInclusionUi = false;
        CameraDebugViewCheckBox.IsEnabled = selected.IsIncludedInVisionPipeline;
        applyingCameraDebugViewUi = true;
        CameraDebugViewCheckBox.IsChecked = NormalizeDebugViewEnabled(selected.IsIncludedInVisionPipeline, selected.DebugViewEnabled);
        applyingCameraDebugViewUi = false;
        RefreshSelectedUsbCameraSourceStatusUi(selected);
        RefreshUsbCaptureSettingsUi(selected);
    }

    private void RefreshCameraWorkspaceTiles(IReadOnlyList<CameraProfile> orderedCameras)
    {
        var projection = BuildCameraGridProjection(orderedCameras
            .Select(camera => new CameraWorkspaceCamera(
                camera.Id,
                camera.DisplayName,
                camera.IsVisible,
                camera.IsIncludedInVisionPipeline,
                NormalizeDebugViewEnabled(camera.IsIncludedInVisionPipeline, camera.DebugViewEnabled)))
            .ToList());

        var viewState = BuildCameraTileViewState(projection);
        ApplyCameraTileViewState(viewState);
        StartCameraTilePreview(orderedCameras, projection);
    }

    private void StartCameraTilePreview(IReadOnlyList<CameraProfile> orderedCameras, CameraGridProjection projection)
    {
        var visibleIds = projection.Tiles.Select(tile => tile.CameraId).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var feedCameras = orderedCameras
            .Where(camera => visibleIds.Contains(camera.Id))
            .Where(camera =>
                !cameraTileRenderModesById.TryGetValue(camera.Id, out var mode) ||
                mode != CameraRenderMode.DebugView)
            .ToList();

        cameraTileFeedCamerasById.Clear();
        foreach (var camera in feedCameras)
        {
            cameraTileFeedCamerasById[camera.Id] = camera;
        }

        var requests = feedCameras
            .Where(camera => cameraTileImagesById.ContainsKey(camera.Id))
            .Select(camera => new CameraTileFeedRequest(
                camera.Id,
                camera.IsUsbCamera ? CameraTileFeedKind.Usb : CameraTileFeedKind.VideoFile))
            .ToList();

        _ = cameraTileFeedCoordinator.ApplyAsync(requests, CancellationToken.None);
    }

    private async Task StopCameraTilePreviewAsync()
    {
        await cameraTileFeedCoordinator.StopAllAsync();
    }

    private IAsyncDisposable StartCameraTileFeedConsumer(CameraTileFeedRequest request)
    {
        if (!cameraTileFeedCamerasById.TryGetValue(request.CameraId, out var camera) ||
            !cameraTileImagesById.TryGetValue(request.CameraId, out var image))
        {
            return EmptyAsyncDisposable.Instance;
        }

        var cts = new CancellationTokenSource();
        var task = Task.Run(() => RunCameraTilePreviewLoop(camera, image, cts.Token), cts.Token);
        return new CameraTileFeedConsumer(cts, task);
    }

    private async Task RunCameraTilePreviewLoop(CameraProfile camera, Image target, CancellationToken cancellationToken)
    {
        try
        {
            if (camera.IsUsbCamera && camera.UsbCamera is { } usb)
            {
                var key = new UsbCameraKey(usb.CameraIndex, usb.Api.ToString().ToUpperInvariant());
                var startupSettings = UsbCaptureSettingsProjection.BuildRawTileStartupSettings(usbCaptureSettingsService.GetRequestedSettings(camera.Id));
                await using var lease = await usbCameraOwnerManager.AcquireAsync(key, startupSettings, cancellationToken);
                var previousVersion = 0L;
                while (!cancellationToken.IsCancellationRequested)
                {
                    var snapshot = await lease.WaitForNextFrameAsync(previousVersion, TimeSpan.FromMilliseconds(250), cancellationToken);
                    if (snapshot is null)
                    {
                        snapshot = lease.LatestFrame;
                    }

                    if (snapshot is not null)
                    {
                        previousVersion = snapshot.Value.FrameVersion;
                        RenderUsbSnapshotToTile(target, snapshot.Value);
                    }

                    await DelayIgnoringCancellationAsync(PreviewIntervalMs, cancellationToken);
                }

                return;
            }

            if (string.IsNullOrWhiteSpace(camera.PrimaryVideoPath))
            {
                return;
            }

            using var videoCapture = new VideoCapture(camera.PrimaryVideoPath);
            if (!videoCapture.IsOpened())
            {
                return;
            }

            var sourceFps = videoCapture.Get(VideoCaptureProperties.Fps);
            var frameIntervalMs = sourceFps > 0.1
                ? Math.Max(1, (int)Math.Round(1000d / sourceFps))
                : PreviewIntervalMs;

            using var videoFrame = new Mat();
            while (!cancellationToken.IsCancellationRequested)
            {
                if (!videoCapture.Read(videoFrame) || videoFrame.Empty())
                {
                    videoCapture.Set(VideoCaptureProperties.PosFrames, 0);
                    await DelayIgnoringCancellationAsync(10, cancellationToken);
                    continue;
                }

                RenderRawFrameToTile(target, videoFrame);
                await DelayIgnoringCancellationAsync(frameIntervalMs, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when tile preview is restarted or stopped.
        }
    }

    private static async Task DelayIgnoringCancellationAsync(int milliseconds, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        await Task.Delay(milliseconds);
    }

    private void RenderRawFrameToTile(Image target, Mat frame)
    {
        var bitmap = ConvertMatToBitmap(frame);
        Dispatcher.UIThread.Post(() => UpdatePreviewBitmap(target, bitmap), DispatcherPriority.Background);
    }

    private void RenderUsbSnapshotToTile(Image target, UsbFrameSnapshot snapshot)
    {
        using var stream = new MemoryStream(snapshot.EncodedJpeg);
        var bitmap = new Bitmap(stream);
        Dispatcher.UIThread.Post(() => UpdatePreviewBitmap(target, bitmap), DispatcherPriority.Background);
    }

    private static Bitmap ConvertMatToBitmap(Mat frame)
    {
        using var rgb = new Mat();
        Cv2.CvtColor(frame, rgb, ColorConversionCodes.BGR2RGB);

        var pixelSize = new PixelSize(rgb.Width, rgb.Height);
        var dpi = new Vector(96, 96);
        var bitmap = new WriteableBitmap(pixelSize, dpi, PixelFormats.Rgb24, AlphaFormat.Opaque);

        using var locked = bitmap.Lock();
        var bytesPerRow = rgb.Width * 3;
        var sourceStride = (int)rgb.Step();
        var destinationStride = locked.RowBytes;
        var rowBuffer = new byte[bytesPerRow];

        for (var row = 0; row < rgb.Height; row++)
        {
            var sourceRow = rgb.Data + (row * sourceStride);
            var destinationRow = locked.Address + (row * destinationStride);
            Marshal.Copy(sourceRow, rowBuffer, 0, bytesPerRow);
            Marshal.Copy(rowBuffer, 0, destinationRow, bytesPerRow);
        }

        return bitmap;
    }

    private static void UpdatePreviewBitmap(Image target, Bitmap bitmap)
    {
        var previous = target.Source as Bitmap;
        target.Source = bitmap;
        previous?.Dispose();
    }

    private void ApplyCameraTileViewState(CameraTileViewState viewState)
    {
        ApplyCameraTileGridDimensions(viewState.Rows, viewState.Columns);

        var existingImagesById = new Dictionary<string, Image>(cameraTileImagesById, StringComparer.OrdinalIgnoreCase);

        CameraTileGrid.Children.Clear();
        cameraTileImagesById.Clear();
        cameraTileRenderModesById.Clear();
        cameraTileDebugImagesById.Clear();

        for (var i = 0; i < viewState.CameraIds.Count; i++)
        {
            var cameraId = viewState.CameraIds[i];
            var image = existingImagesById.TryGetValue(cameraId, out var existingImage)
                ? existingImage
                : new Image { Stretch = Avalonia.Media.Stretch.Uniform };
            cameraTileImagesById[cameraId] = image;
            cameraTileRenderModesById[cameraId] = viewState.RenderModes[i];

            var panel = new Panel();
            if (image.Parent is Panel currentParent)
            {
                currentParent.Children.Remove(image);
            }

            panel.Children.Add(image);

            if (viewState.RenderModes[i] == CameraRenderMode.DebugView)
            {
                var debugGrid = new Grid
                {
                    RowDefinitions = new RowDefinitions("*,*"),
                    ColumnDefinitions = new ColumnDefinitions("*,*")
                };

                var debugBackground = new Image { Stretch = Avalonia.Media.Stretch.UniformToFill };
                debugGrid.Children.Add(debugBackground);

                var debugMoving = new Image { Stretch = Avalonia.Media.Stretch.UniformToFill };
                Grid.SetColumn(debugMoving, 1);
                debugGrid.Children.Add(debugMoving);

                var debugColor = new Image { Stretch = Avalonia.Media.Stretch.UniformToFill };
                Grid.SetRow(debugColor, 1);
                debugGrid.Children.Add(debugColor);

                var debugMotion = new Image { Stretch = Avalonia.Media.Stretch.UniformToFill };
                Grid.SetRow(debugMotion, 1);
                Grid.SetColumn(debugMotion, 1);
                debugGrid.Children.Add(debugMotion);

                cameraTileDebugImagesById[viewState.CameraIds[i]] = new DebugTileImageSet(
                    debugBackground,
                    debugMoving,
                    debugColor,
                    debugMotion);

                debugGrid.Children.Add(new Border { BorderBrush = Avalonia.Media.Brush.Parse("#66FFFFFF"), BorderThickness = new Thickness(1), Margin = new Thickness(0) });
                var topRight = new Border { BorderBrush = Avalonia.Media.Brush.Parse("#66FFFFFF"), BorderThickness = new Thickness(1), Margin = new Thickness(0) };
                Grid.SetColumn(topRight, 1);
                debugGrid.Children.Add(topRight);
                var bottomLeft = new Border { BorderBrush = Avalonia.Media.Brush.Parse("#66FFFFFF"), BorderThickness = new Thickness(1), Margin = new Thickness(0) };
                Grid.SetRow(bottomLeft, 1);
                debugGrid.Children.Add(bottomLeft);
                var bottomRight = new Border { BorderBrush = Avalonia.Media.Brush.Parse("#66FFFFFF"), BorderThickness = new Thickness(1), Margin = new Thickness(0) };
                Grid.SetRow(bottomRight, 1);
                Grid.SetColumn(bottomRight, 1);
                debugGrid.Children.Add(bottomRight);

                panel.Children.Add(debugGrid);
            }

            var modeBadge = new TextBlock
            {
                Text = GetCameraRenderModeBadge(viewState.RenderModes[i]),
                FontSize = 10,
                FontWeight = Avalonia.Media.FontWeight.SemiBold,
                Foreground = Avalonia.Media.Brush.Parse("#EAF4FF")
            };

            var modeBadgeOverlay = new Border
            {
                VerticalAlignment = VerticalAlignment.Bottom,
                HorizontalAlignment = HorizontalAlignment.Left,
                Background = Avalonia.Media.Brush.Parse("#7F000000"),
                Padding = new Thickness(6, 3),
                Margin = new Thickness(6),
                Child = modeBadge
            };

            panel.Children.Add(modeBadgeOverlay);

            if (TryGetCameraById(cameraId, out var tileCamera))
            {
                var statusView = BuildUsbCameraSourceStatusView(tileCamera);
                if (statusView.ShowPlaceholder)
                {
                    panel.Children.Add(new Border
                    {
                        Background = Avalonia.Media.Brush.Parse("#CC111820"),
                        Child = new TextBlock
                        {
                            Text = statusView.StatusText,
                            Foreground = Avalonia.Media.Brush.Parse("#EAF4FF"),
                            FontWeight = Avalonia.Media.FontWeight.SemiBold,
                            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center,
                            Margin = new Thickness(12)
                        }
                    });
                }
            }

            if (viewState.RenderModes[i] != CameraRenderMode.RawFeed)
            {
                var title = new TextBlock
                {
                    Text = viewState.Titles[i],
                    Classes = { "cardTitle" }
                };

                var titleOverlay = new Border
                {
                    VerticalAlignment = VerticalAlignment.Top,
                    Background = Avalonia.Media.Brush.Parse("#99000000"),
                    Padding = new Thickness(8, 5),
                    Child = title
                };

                panel.Children.Add(titleOverlay);
            }

            CameraTileGrid.Children.Add(new Border
            {
                Background = Avalonia.Media.Brush.Parse("#070A0E"),
                Child = panel
            });
        }
    }

    private void CameraVisibilityCheckBoxOnChanged(object? sender, RoutedEventArgs e)
    {
        if (applyingCameraVisibilityUi)
        {
            return;
        }

        lock (cameraSync)
        {
            if (selectedCameraIndex < 0 || selectedCameraIndex >= cameras.Count)
            {
                return;
            }

            var selected = cameras[selectedCameraIndex];
            var isVisible = CameraVisibilityCheckBox.IsChecked == true;
            cameras[selectedCameraIndex] = selected with { IsVisible = isVisible };
        }

        RefreshCameraUi();
    }

    private async void RestartUsbCameraSourceButtonOnClick(object? sender, RoutedEventArgs e)
    {
        var camera = GetSelectedCamera();
        if (camera is not { IsUsbCamera: true, UsbCamera: { } usb })
        {
            return;
        }

        var key = new UsbCameraKey(usb.CameraIndex, usb.Api.ToString().ToUpperInvariant());
        try
        {
            SetStatus($"Status: restarting {camera.Value.DisplayName}...");
            RefreshSelectedUsbCameraSourceStatusUi(camera.Value);
            await usbCameraOwnerManager.RestartAsync(key, ToUsbCaptureSettings(usbCaptureSettingsService.GetRequestedSettings(camera.Value.Id)), CancellationToken.None);
            RefreshSelectedUsbCameraSourceStatusUi(camera.Value);
            RefreshCameraUi();
            SetStatus($"Status: restarted {camera.Value.DisplayName}.");
        }
        catch (Exception ex)
        {
            RefreshSelectedUsbCameraSourceStatusUi(camera.Value);
            RefreshCameraUi();
            SetStatus($"Status: failed to restart {camera.Value.DisplayName} - {ex.Message}");
        }
    }

    private void RefreshSelectedUsbCameraSourceStatusUi(CameraProfile camera)
    {
        var statusView = BuildUsbCameraSourceStatusView(camera);
        UsbCameraSourceStatusText.IsVisible = camera.IsUsbCamera;
        RestartUsbCameraSourceButton.IsVisible = camera.IsUsbCamera;
        UsbCameraSourceStatusText.Text = statusView.StatusText;
        RestartUsbCameraSourceButton.IsEnabled = statusView.RestartEnabled;
        RestartUsbCameraSourceButton.Tag = statusView.RestartDisabledReason;
    }

    private void InitializeUsbCaptureSettingsUi()
    {
        UsbResolutionComboBox.ItemsSource = UsbCaptureSettingsService.ResolutionPresets.ToList();
        UsbTargetFpsComboBox.ItemsSource = UsbCaptureSettingsService.TargetFpsPresets.ToList();
    }

    private void RefreshUsbCaptureSettingsUi(CameraProfile camera)
    {
        var requested = usbCaptureSettingsService.GetDraftSettings(camera.Id);
        var statusView = BuildUsbCameraSourceStatusView(camera);
        var usbStatus = camera is { IsUsbCamera: true, UsbCamera: { } usb }
            ? usbCameraOwnerManager.GetStatus(new UsbCameraKey(usb.CameraIndex, usb.Api.ToString().ToUpperInvariant()), DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
            : new UsbCameraRuntimeStatus(UsbCameraOwnerState.Stopped, false, null, null, null, null, null);
        var projection = UsbCaptureSettingsProjection.Build(camera.IsUsbCamera, camera.IsVisible, usbStatus, requested);

        UsbCaptureSettingsPanel.IsVisible = projection.IsVisible;
        UsbCaptureModeStatusText.Text = projection.ModeStatusText;
        if (!projection.IsVisible)
        {
            return;
        }

        applyingUsbCaptureSettingsUi = true;
        UsbResolutionComboBox.SelectedItem = new UsbResolutionPreset(requested.Width, requested.Height);
        UsbTargetFpsComboBox.SelectedItem = requested.TargetFps;
        applyingUsbCaptureSettingsUi = false;
        ApplyUsbCaptureSettingsButton.IsEnabled = true;
        RevertUsbCaptureSettingsButton.IsEnabled = true;
    }

    private void UsbCaptureSettingsControlOnChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (applyingUsbCaptureSettingsUi)
        {
            return;
        }

        var camera = GetSelectedCamera();
        if (camera is not { IsUsbCamera: true })
        {
            return;
        }

        usbCaptureSettingsService.UpdateDraft(camera.Value.Id, ReadUsbCaptureSettingsDraft(camera.Value.Id));
        RefreshUsbCaptureSettingsUi(camera.Value);
    }

    private async void ApplyUsbCaptureSettingsButtonOnClick(object? sender, RoutedEventArgs e)
    {
        var camera = GetSelectedCamera();
        if (camera is not { IsUsbCamera: true, UsbCamera: { } usb })
        {
            return;
        }

        usbCaptureSettingsService.UpdateDraft(camera.Value.Id, ReadUsbCaptureSettingsDraft(camera.Value.Id));
        var result = usbCaptureSettingsService.ApplyDraft(camera.Value.Id);
        usbCaptureSettingsStore.Save(result.SettingsByCameraSourceId);

        var key = new UsbCameraKey(usb.CameraIndex, usb.Api.ToString().ToUpperInvariant());
        var status = usbCameraOwnerManager.GetStatus(key, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        var decision = UsbCaptureSettingsProjection.BuildApplyDecision(
            camera.Value.IsUsbCamera,
            camera.Value.IsVisible,
            camera.Value.IsIncludedInVisionPipeline,
            runTask is not null,
            status);
        if (decision.ShouldRestartCameraSource)
        {
            await usbCameraOwnerManager.RestartAsync(key, ToUsbCaptureSettings(usbCaptureSettingsService.GetRequestedSettings(camera.Value.Id)), CancellationToken.None);
            SetStatus($"Status: applied USB capture settings for {camera.Value.DisplayName}.");
        }
        else if (decision.RequiresVisionPipelineRestart)
        {
            pendingUsbCaptureSettingsCameraSourceIds.Add(camera.Value.Id);
            hasPendingVisionPipelineRestart = true;
            UpdateBottomStatusBar();
            SetStatus($"Status: {decision.Message}");
        }
        else if (!string.IsNullOrWhiteSpace(decision.Message))
        {
            SetStatus($"Status: USB capture settings saved. {decision.Message}");
        }
        else
        {
            SetStatus($"Status: USB capture settings saved for {camera.Value.DisplayName}.");
        }

        RefreshSelectedUsbCameraSourceStatusUi(camera.Value);
        RefreshUsbCaptureSettingsUi(camera.Value);
    }

    private void RevertUsbCaptureSettingsButtonOnClick(object? sender, RoutedEventArgs e)
    {
        var camera = GetSelectedCamera();
        if (camera is not { IsUsbCamera: true })
        {
            return;
        }

        usbCaptureSettingsService.RevertDraft(camera.Value.Id);
        RefreshUsbCaptureSettingsUi(camera.Value);
        SetStatus($"Status: reverted USB capture settings for {camera.Value.DisplayName}.");
    }

    private UsbCaptureSettingsRequest ReadUsbCaptureSettingsDraft(string cameraSourceId)
    {
        var current = usbCaptureSettingsService.GetDraftSettings(cameraSourceId);
        var resolution = UsbResolutionComboBox.SelectedItem is UsbResolutionPreset selectedResolution
            ? selectedResolution
            : new UsbResolutionPreset(current.Width, current.Height);
        var targetFps = UsbTargetFpsComboBox.SelectedItem is int selectedFps ? selectedFps : current.TargetFps;
        return new UsbCaptureSettingsRequest(resolution.Width, resolution.Height, targetFps);
    }

    private static UsbCaptureSettings ToUsbCaptureSettings(UsbCaptureSettingsRequest request)
    {
        return new UsbCaptureSettings(request.Width, request.Height, request.TargetFps);
    }

    private CameraSourceStatusView BuildUsbCameraSourceStatusView(CameraProfile camera)
    {
        if (!camera.IsUsbCamera || camera.UsbCamera is not { } usb)
        {
            return CameraSourceStatusProjection.BuildUsbStatus(
                isUsbCameraSource: false,
                isActivelyProcessedByVisionPipeline: false,
                new UsbCameraRuntimeStatus(UsbCameraOwnerState.Stopped, false, null, null, null, null, null));
        }

        var key = new UsbCameraKey(usb.CameraIndex, usb.Api.ToString().ToUpperInvariant());
        var status = usbCameraOwnerManager.GetStatus(key, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        return CameraSourceStatusProjection.BuildUsbStatus(
            isUsbCameraSource: true,
            isActivelyProcessedByVisionPipeline: runTask is not null,
            status);
    }

    private bool TryGetCameraById(string cameraId, out CameraProfile camera)
    {
        lock (cameraSync)
        {
            foreach (var candidate in cameras)
            {
                if (string.Equals(candidate.Id, cameraId, StringComparison.OrdinalIgnoreCase))
                {
                    camera = candidate;
                    return true;
                }
            }
        }

        camera = default;
        return false;
    }

    private void VisionPipelineInclusionCheckBoxOnChanged(object? sender, RoutedEventArgs e)
    {
        if (applyingCameraInclusionUi)
        {
            return;
        }

        lock (cameraSync)
        {
            if (selectedCameraIndex < 0 || selectedCameraIndex >= cameras.Count)
            {
                return;
            }

            var selected = cameras[selectedCameraIndex];
            var isIncluded = VisionPipelineInclusionCheckBox.IsChecked == true;
            cameras[selectedCameraIndex] = selected with
            {
                IsIncludedInVisionPipeline = isIncluded,
                DebugViewEnabled = NormalizeDebugViewEnabled(isIncluded, selected.DebugViewEnabled)
            };
        }

        applyingCameraDebugViewUi = true;
        CameraDebugViewCheckBox.IsEnabled = VisionPipelineInclusionCheckBox.IsChecked == true;
        CameraDebugViewCheckBox.IsChecked = VisionPipelineInclusionCheckBox.IsChecked == true && CameraDebugViewCheckBox.IsChecked == true;
        applyingCameraDebugViewUi = false;

        RefreshCameraUi();
    }

    private void CameraDebugViewCheckBoxOnChanged(object? sender, RoutedEventArgs e)
    {
        if (applyingCameraDebugViewUi)
        {
            return;
        }

        lock (cameraSync)
        {
            if (selectedCameraIndex < 0 || selectedCameraIndex >= cameras.Count)
            {
                return;
            }

            var selected = cameras[selectedCameraIndex];
            var debugEnabled = CameraDebugViewCheckBox.IsChecked == true;
            cameras[selectedCameraIndex] = selected with
            {
                DebugViewEnabled = NormalizeDebugViewEnabled(selected.IsIncludedInVisionPipeline, debugEnabled)
            };
        }

        RefreshCameraUi();
    }

    private void ApplyCameraTileGridDimensions(int rows, int columns)
    {
        CameraTileGrid.Rows = Math.Max(1, rows);
        CameraTileGrid.Columns = Math.Max(1, columns);
    }

    private Dictionary<string, string> BuildCameraDisplayNamesBySourceId()
    {
        lock (cameraSync)
        {
            return cameras.ToDictionary(camera => camera.Id, camera => camera.DisplayName, StringComparer.OrdinalIgnoreCase);
        }
    }

    private void SetRunState(bool isRunning)
    {
        StartStopButton.Content = isRunning ? "Stop" : "Start";
        StartStopButton.IsEnabled = isRunning;
        var menuState = BuildVisionPipelineMenuState(isRunning);
        StartVisionPipelineMenuItem.IsEnabled = menuState.StartEnabled;
        StopVisionPipelineMenuItem.IsEnabled = menuState.StopEnabled;
        AddVideosButton.IsEnabled = !isRunning;
        RemoveSelectedButton.IsEnabled = !isRunning;
        LoopPlaylistCheckBox.IsEnabled = !isRunning;

        if (!isRunning)
        {
            Interlocked.Exchange(ref requestedCameraIndex, -1);
        }

        ApplyCameraDestructiveActionsState(GetCameraCount());

        UpdateBottomStatusBar();
    }

    private void ApplyCameraDestructiveActionsState(int cameraCount)
    {
        var selectedIndex = PlaylistListBox.SelectedIndex;
        if ((selectedIndex < 0 || selectedIndex >= cameraCount) && selectedCameraIndex >= 0 && selectedCameraIndex < cameraCount)
        {
            selectedIndex = selectedCameraIndex;
        }

        if ((selectedIndex < 0 || selectedIndex >= cameraCount) && cameraCount == 1)
        {
            selectedIndex = 0;
        }

        var hasSelectedCamera = selectedIndex >= 0 && selectedIndex < cameraCount;
        var state = BuildCameraDestructiveActionsState(
            isVisionPipelineRunning: runTask is not null,
            hasSelectedCamera: hasSelectedCamera,
            cameraCount: cameraCount);

        RemoveSelectedButton.IsEnabled = state.DeleteSelectedEnabled;
        DeleteSelectedCameraMenuItem.IsEnabled = state.DeleteSelectedEnabled;
        ClearCamerasMenuItem.IsEnabled = state.ClearAllEnabled;
    }

    private bool TryResolveSelectedCameraForDestructiveAction(out int index, out CameraProfile camera)
    {
        index = -1;
        camera = default;

        lock (cameraSync)
        {
            var count = cameras.Count;
            if (count <= 0)
            {
                return false;
            }

            var candidate = PlaylistListBox.SelectedIndex;
            if (candidate < 0 || candidate >= count)
            {
                candidate = selectedCameraIndex;
            }

            if ((candidate < 0 || candidate >= count) && count == 1)
            {
                candidate = 0;
            }

            if (candidate < 0 || candidate >= count)
            {
                return false;
            }

            selectedCameraIndex = candidate;
            index = candidate;
            camera = cameras[candidate];
            return true;
        }
    }

    private async Task<bool> ShowDestructiveConfirmationDialogAsync(string title, string message)
    {
        var dialog = new Avalonia.Controls.Window
        {
            Width = 480,
            Height = 180,
            CanResize = false,
            Title = title,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var result = false;
        var cancelButton = new Button { Content = "Cancel", MinWidth = 90 };
        var confirmButton = new Button { Content = "Confirm", MinWidth = 90, Classes = { "destructive" } };
        cancelButton.Click += (_, _) => dialog.Close();
        confirmButton.Click += (_, _) =>
        {
            result = true;
            dialog.Close();
        };

        var contentGrid = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,12,Auto")
        };
        contentGrid.Children.Add(new TextBlock
        {
            Text = message,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap
        });
        var buttonRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Children = { cancelButton, confirmButton }
        };
        Grid.SetRow(buttonRow, 2);
        contentGrid.Children.Add(buttonRow);

        dialog.Content = new Border
        {
            Padding = new Thickness(14),
            Child = contentGrid
        };

        await dialog.ShowDialog(this);
        return result;
    }

    private async Task<SettingsNavigationDecision> ShowSettingsNavigationGuardDialogAsync()
    {
        var dialog = new Avalonia.Controls.Window
        {
            Width = 540,
            Height = 190,
            CanResize = false,
            Title = "Unsaved Settings",
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var result = SettingsNavigationDecision.Cancel;
        var saveButton = new Button { Content = "Save", MinWidth = 90, Classes = { "primary" } };
        var discardButton = new Button { Content = "Discard", MinWidth = 90, Classes = { "destructive" } };
        var cancelButton = new Button { Content = "Cancel", MinWidth = 90 };
        saveButton.Click += (_, _) =>
        {
            result = SettingsNavigationDecision.Save;
            dialog.Close();
        };
        discardButton.Click += (_, _) =>
        {
            result = SettingsNavigationDecision.Discard;
            dialog.Close();
        };
        cancelButton.Click += (_, _) => dialog.Close();

        var contentGrid = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,12,Auto")
        };
        contentGrid.Children.Add(new TextBlock
        {
            Text = "Settings have unsaved changes. Save changes, discard them, or cancel navigation?",
            TextWrapping = Avalonia.Media.TextWrapping.Wrap
        });
        var buttonRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Children = { saveButton, discardButton, cancelButton }
        };
        Grid.SetRow(buttonRow, 2);
        contentGrid.Children.Add(buttonRow);

        dialog.Content = new Border
        {
            Padding = new Thickness(14),
            Child = contentGrid
        };

        await dialog.ShowDialog(this);
        return result;
    }

    private void UpdateBottomStatusBar()
    {
        var snapshot = BuildBottomStatusSnapshot(runTask is not null, hasPendingVisionPipelineRestart);
        BottomVisionPipelineStateText.Text = snapshot.VisionPipeline;
        BottomCalibrationStateText.Text = snapshot.Calibration;
        BottomPendingRestartStateText.Text = snapshot.PendingRestart;
    }

    private void RuntimeSettingControlOnLostFocus(object? sender, RoutedEventArgs e)
    {
        UpdateSelectedCameraSettingsFromUi(logChange: true);
    }

    private void CalibrationColorComboBoxOnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var selectedName = GetCalibrationSelectionName();
        if (string.IsNullOrWhiteSpace(selectedName))
        {
            return;
        }

        selectedCalibrationColor = selectedName;

        var camera = GetSelectedCamera();
        var settings = camera is null
            ? RuntimeProcessingSettings.Default
            : GetSettingsForCamera(camera.Value.Id);

        LoadCalibrationEditor(settings.ColorCalibrations, selectedName);
    }

    private void ColorCalibrationControlOnLostFocus(object? sender, RoutedEventArgs e)
    {
        UpdateSelectedColorCalibrationFromUi(logChange: true);
    }

    private void UpdateSelectedCameraSettingsFromUi(bool logChange)
    {
        var camera = GetSelectedCamera();
        if (camera is null)
        {
            return;
        }

        var sampleCount = ParseInt(SampleCountTextBox.Text, 20, 5, 200);
        var threshold = ParseInt(ThresholdTextBox.Text, 100, 1, 255);
        var motionArea = ParseInt(MotionAreaTextBox.Text, 220, 20, 100000);
        var colorMinPixels = ParseInt(ColorMinPixelsTextBox.Text, 40, 1, 100000);
        var morphKernelSize = ParseOddInt(MorphKernelSizeTextBox.Text, 3, 1, 31);
        var processMaxWidth = ParseInt(ProcessWidthTextBox.Text, 640, 160, 1920);
        var bakeSourceMode = ParseBakeSourceMode();
        var bakeImagePath = (BakeImagePathTextBox.Text ?? string.Empty).Trim();

        SampleCountTextBox.Text = sampleCount.ToString();
        ThresholdTextBox.Text = threshold.ToString();
        MotionAreaTextBox.Text = motionArea.ToString();
        ColorMinPixelsTextBox.Text = colorMinPixels.ToString();
        MorphKernelSizeTextBox.Text = morphKernelSize.ToString();
        ProcessWidthTextBox.Text = processMaxWidth.ToString();
        BakeImagePathTextBox.Text = bakeImagePath;

        var existing = GetSettingsForCamera(camera.Value.Id);
        var colorCalibrations = BuildCalibrationsFromEditor(existing.ColorCalibrations);

        lock (settingsSync)
        {
            cameraSettings[camera.Value.Id] = new RuntimeProcessingSettings(
                sampleCount,
                threshold,
                motionArea,
                colorMinPixels,
                morphKernelSize,
                processMaxWidth,
                bakeSourceMode,
                bakeImagePath,
                colorCalibrations);
        }

        PersistCameraSettings();
        UpdateOpenBakedMaskButtonState(camera.Value, cameraSettings[camera.Value.Id]);

        if (runTask is not null)
        {
            hasPendingVisionPipelineRestart = true;
            UpdateBottomStatusBar();
        }

        if (logChange)
        {
            SetStatus($"Status: camera {camera.Value.DisplayName} settings updated.");
        }
    }

    private void ApplySettingsToUi(RuntimeProcessingSettings settings)
    {
        SampleCountTextBox.Text = settings.SampleCount.ToString();
        ThresholdTextBox.Text = settings.Threshold.ToString();
        MotionAreaTextBox.Text = settings.MotionArea.ToString();
        ColorMinPixelsTextBox.Text = settings.ColorMinPixels.ToString();
        MorphKernelSizeTextBox.Text = settings.MorphKernelSize.ToString();
        ProcessWidthTextBox.Text = settings.ProcessMaxWidth.ToString();
        BakeSourceComboBox.SelectedIndex = (int)settings.BakeSourceMode;
        BakeImagePathTextBox.Text = settings.BakeImagePath;
        ApplyBakeSourceUiState();

        var selectedColor = GetCalibrationSelectionName() ?? selectedCalibrationColor;
        selectedCalibrationColor = selectedColor;
        LoadCalibrationEditor(settings.ColorCalibrations, selectedColor);
    }

    private RuntimeProcessingSettings GetSettingsForCamera(string cameraId)
    {
        lock (settingsSync)
        {
            if (cameraSettings.TryGetValue(cameraId, out var settings))
            {
                return settings;
            }

            return RuntimeProcessingSettings.Default;
        }
    }

    private BackgroundEstimationEngine.LiveTuning GetLiveTuningForCamera(string cameraId)
    {
        var settings = GetSettingsForCamera(cameraId);
        return new BackgroundEstimationEngine.LiveTuning(
            settings.Threshold,
            settings.MotionArea,
            settings.ColorMinPixels,
            settings.MorphKernelSize,
            settings.ColorCalibrations);
    }

    private void PersistCameraSettings()
    {
        Dictionary<string, RuntimeProcessingSettings> snapshot;
        lock (settingsSync)
        {
            snapshot = cameraSettings.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        }

        cameraSettingsStore.Save(snapshot);
    }

    private void PersistCameraZones()
    {
        cameraZoneBindingStore.Save(cameraZoneIdentityService.CameraZones, cameraZoneIdentityService.SourceBindings);
    }

    private CameraProfile? GetSelectedCamera()
    {
        lock (cameraSync)
        {
            if (selectedCameraIndex < 0 || selectedCameraIndex >= cameras.Count)
            {
                return null;
            }

            return cameras[selectedCameraIndex];
        }
    }

    private int GetCameraCount()
    {
        lock (cameraSync)
        {
            return cameras.Count;
        }
    }

    private bool TryGetCamera(int index, out CameraProfile camera)
    {
        lock (cameraSync)
        {
            if (index < 0 || index >= cameras.Count)
            {
                camera = default;
                return false;
            }

            camera = cameras[index];
            return true;
        }
    }

    private void NavigateCameraRelative(int delta)
    {
        var count = GetCameraCount();
        if (count == 0)
        {
            return;
        }

        var current = selectedCameraIndex;
        if (current < 0 || current >= count)
        {
            current = Math.Clamp(PlaylistListBox.SelectedIndex, 0, count - 1);
        }

        var target = current + delta;
        if (target < 0)
        {
            target = count - 1;
        }
        else if (target >= count)
        {
            target = 0;
        }

        selectedCameraIndex = target;
        PlaylistListBox.SelectedIndex = target;

        if (runTask is not null)
        {
            Interlocked.Exchange(ref requestedCameraIndex, target);
            SetStatus($"Status: switching to camera #{target + 1}...");
            if (TryGetCamera(target, out var requestedCamera))
            {
                sessionAuditLogger.AppendEvent(
                    SessionAuditLogger.EventCameraSwitch,
                    "Camera switch requested from navigation buttons.",
                    ("cameraId", requestedCamera.Id),
                    ("cameraName", requestedCamera.DisplayName),
                    ("requestedIndex", target.ToString()));
            }

            return;
        }

        if (TryGetCamera(target, out var camera))
        {
            ApplySettingsToUi(GetSettingsForCamera(camera.Id));
            CurrentVideoText.Text = BuildCurrentSourceText(camera);
            OpenBakedMaskButton.IsEnabled = camera.CanOpenBakedMask;
            SetStatus($"Status: selected camera {camera.DisplayName}");
        }
    }

    private IReadOnlyList<string> GetAddedUsbCameraSourceIds()
    {
        lock (cameraSync)
        {
            return cameras
                .Where(camera => camera.IsUsbCamera)
                .Select(camera => camera.Id)
                .ToList();
        }
    }

    private static IReadOnlyList<UsbCameraOption> DiscoverUsbCameraOptions(IReadOnlyCollection<string> alreadyAddedSourceIds)
    {
        var api = GetDefaultUsbCaptureApi();
        var discovery = new UsbCameraDiscoveryService(MaxUsbCameraProbeIndex, api, cameraIndex => ProbeUsbCamera(cameraIndex, api));
        return discovery.DiscoverUsbCameraOptions(alreadyAddedSourceIds);
    }

    private static UsbCameraProbeResult ProbeUsbCamera(int cameraIndex, VideoCaptureAPIs api)
    {
        using var capture = new VideoCapture(cameraIndex, api);
        capture.Set(VideoCaptureProperties.BufferSize, 1);
        if (!capture.IsOpened())
        {
            return UsbCameraProbeResult.Unavailable;
        }

        var width = (int)Math.Round(capture.Get(VideoCaptureProperties.FrameWidth));
        var height = (int)Math.Round(capture.Get(VideoCaptureProperties.FrameHeight));
        return new UsbCameraProbeResult(true, width, height);
    }

    private static VideoCaptureAPIs GetDefaultUsbCaptureApi()
    {
        return OperatingSystem.IsWindows()
            ? VideoCaptureAPIs.DSHOW
            : VideoCaptureAPIs.ANY;
    }

    private static string BuildCurrentSourceText(CameraProfile camera, string? activeSourceLabel = null)
    {
        var sourceLabel = string.IsNullOrWhiteSpace(activeSourceLabel)
            ? camera.CurrentSourceLabel
            : activeSourceLabel;

        return $"Current camera/source: {camera.DisplayName} / {sourceLabel}";
    }

    private void ApplyBakeSourceUiState()
    {
        var bakeSourceMode = ParseBakeSourceMode();
        var usesImage = bakeSourceMode == BakeSourceMode.ImageFile;

        SampleCountTextBox.IsEnabled = !usesImage;
        SelectBakeImageButton.IsEnabled = usesImage;
        BakeImagePathTextBox.IsEnabled = usesImage;
        ClearBakeImageButton.IsEnabled = usesImage && !string.IsNullOrWhiteSpace(BakeImagePathTextBox.Text);
    }

    private BakeSourceMode ParseBakeSourceMode()
    {
        return BakeSourceComboBox.SelectedIndex == (int)BakeSourceMode.ImageFile
            ? BakeSourceMode.ImageFile
            : BakeSourceMode.Samples;
    }

    private static string? GetBakeImagePath(RuntimeProcessingSettings settings)
    {
        if (settings.BakeSourceMode != BakeSourceMode.ImageFile || string.IsNullOrWhiteSpace(settings.BakeImagePath))
        {
            return null;
        }

        return settings.BakeImagePath;
    }

    private void UpdateOpenBakedMaskButtonState(CameraProfile camera, RuntimeProcessingSettings settings)
    {
        OpenBakedMaskButton.IsEnabled = !camera.IsUsbCamera
            && (settings.BakeSourceMode == BakeSourceMode.Samples || !string.IsNullOrWhiteSpace(settings.BakeImagePath));
    }

    private void SetStatus(string text)
    {
        StatusText.Text = text;
        AppendLog(text);
    }

    private void AppendLog(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        var line = $"[{timestamp}] {message}";

        logEntries.Add(line);
        sessionAuditLogger.AppendStatus(message);
        while (logEntries.Count > MaxLogEntries)
        {
            logEntries.RemoveAt(0);
        }

        if (!LogListBox.IsEffectivelyVisible)
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            if (!LogListBox.IsEffectivelyVisible)
            {
                return;
            }

            try
            {
                LogListBox.ScrollIntoView(line);
            }
            catch (InvalidOperationException)
            {
                // Ignore transient layout instability while the list is being arranged.
            }
        }, DispatcherPriority.Background);
    }

    private void UpdateSelectedColorCalibrationFromUi(bool logChange)
    {
        var camera = GetSelectedCamera();
        if (camera is null)
        {
            return;
        }

        var settings = GetSettingsForCamera(camera.Value.Id);
        var updatedCalibrations = BuildCalibrationsFromEditor(settings.ColorCalibrations);

        lock (settingsSync)
        {
            cameraSettings[camera.Value.Id] = settings with { ColorCalibrations = updatedCalibrations };
        }

        PersistCameraSettings();

        if (logChange)
        {
            SetStatus($"Status: {camera.Value.DisplayName} color calibration updated for {selectedCalibrationColor}.");
        }

        var selectedProfile = updatedCalibrations.FirstOrDefault(profile => profile.Name.Equals(selectedCalibrationColor, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(selectedProfile.Name))
        {
            sessionAuditLogger.AppendEvent(
                SessionAuditLogger.EventCalibrationChange,
                "Color calibration updated.",
                ("cameraId", camera.Value.Id),
                ("cameraName", camera.Value.DisplayName),
                ("color", selectedProfile.Name),
                ("hMin", selectedProfile.HueLower.ToString()),
                ("hMax", selectedProfile.HueUpper.ToString()),
                ("sMin", selectedProfile.SaturationLower.ToString()),
                ("sMax", selectedProfile.SaturationUpper.ToString()),
                ("vMin", selectedProfile.ValueLower.ToString()),
                ("vMax", selectedProfile.ValueUpper.ToString()));
        }
    }

    private IReadOnlyList<ColorCalibrationProfile> BuildCalibrationsFromEditor(IReadOnlyList<ColorCalibrationProfile> source)
    {
        var selectedColor = GetCalibrationSelectionName() ?? selectedCalibrationColor;
        selectedCalibrationColor = selectedColor;

        var fallback = source.FirstOrDefault(profile => profile.Name.Equals(selectedColor, StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrWhiteSpace(fallback.Name))
        {
            fallback = CreateDefaultColorCalibrations().First(profile => profile.Name.Equals(selectedColor, StringComparison.OrdinalIgnoreCase));
        }

        var updated = NormalizeColorCalibration(new ColorCalibrationProfile(
            selectedColor,
            ParseInt(HueLowerTextBox.Text, fallback.HueLower, 0, 180),
            ParseInt(HueUpperTextBox.Text, fallback.HueUpper, 0, 180),
            ParseInt(SaturationLowerTextBox.Text, fallback.SaturationLower, 0, 255),
            ParseInt(SaturationUpperTextBox.Text, fallback.SaturationUpper, 0, 255),
            ParseInt(ValueLowerTextBox.Text, fallback.ValueLower, 0, 255),
            ParseInt(ValueUpperTextBox.Text, fallback.ValueUpper, 0, 255)));

        HueLowerTextBox.Text = updated.HueLower.ToString();
        HueUpperTextBox.Text = updated.HueUpper.ToString();
        SaturationLowerTextBox.Text = updated.SaturationLower.ToString();
        SaturationUpperTextBox.Text = updated.SaturationUpper.ToString();
        ValueLowerTextBox.Text = updated.ValueLower.ToString();
        ValueUpperTextBox.Text = updated.ValueUpper.ToString();

        var result = source
            .Where(profile => !string.IsNullOrWhiteSpace(profile.Name))
            .Select(NormalizeColorCalibration)
            .Where(profile => !profile.Name.Equals(selectedColor, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(profile => profile.Name, profile => profile, StringComparer.OrdinalIgnoreCase);

        result[selectedColor] = updated;

        foreach (var defaults in CreateDefaultColorCalibrations())
        {
            if (!result.ContainsKey(defaults.Name))
            {
                result[defaults.Name] = defaults;
            }
        }

        return result.Values
            .OrderBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void LoadCalibrationEditor(IReadOnlyList<ColorCalibrationProfile> calibrations, string selectedColor)
    {
        selectedCalibrationColor = selectedColor;
        var profile = calibrations.FirstOrDefault(item => item.Name.Equals(selectedColor, StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrWhiteSpace(profile.Name))
        {
            profile = CreateDefaultColorCalibrations().First(item => item.Name.Equals(selectedColor, StringComparison.OrdinalIgnoreCase));
        }

        HueLowerTextBox.Text = profile.HueLower.ToString();
        HueUpperTextBox.Text = profile.HueUpper.ToString();
        SaturationLowerTextBox.Text = profile.SaturationLower.ToString();
        SaturationUpperTextBox.Text = profile.SaturationUpper.ToString();
        ValueLowerTextBox.Text = profile.ValueLower.ToString();
        ValueUpperTextBox.Text = profile.ValueUpper.ToString();
    }

    private string? GetCalibrationSelectionName()
    {
        if (CalibrationColorComboBox.SelectedItem is ComboBoxItem item
            && item.Content is string selected
            && !string.IsNullOrWhiteSpace(selected))
        {
            return selected.Trim().ToUpperInvariant();
        }

        return null;
    }

    private static ColorCalibrationProfile NormalizeColorCalibration(ColorCalibrationProfile profile)
    {
        return new ColorCalibrationProfile(
            profile.Name.Trim().ToUpperInvariant(),
            Math.Clamp(profile.HueLower, 0, 180),
            Math.Clamp(profile.HueUpper, 0, 180),
            Math.Clamp(profile.SaturationLower, 0, 255),
            Math.Clamp(profile.SaturationUpper, 0, 255),
            Math.Clamp(profile.ValueLower, 0, 255),
            Math.Clamp(profile.ValueUpper, 0, 255));
    }

    internal static IReadOnlyList<ColorCalibrationProfile> CreateDefaultColorCalibrations()
    {
        return new List<ColorCalibrationProfile>
        {
            new ("red", 170, 10, 120, 255, 70, 255),
            new ("green", 35, 85, 80, 255, 60, 255),
            new ("blue", 90, 130, 100, 255, 60, 255),
            new ("yellow", 20, 35, 110, 255, 80, 255),
            new ("white", 0, 180, 0, 50, 190, 255),
            new ("black", 0, 180, 0, 255, 0, 45)
        };
    }

    private static int ParseInt(string? text, int fallback, int min, int max)
    {
        if (!int.TryParse(text, out var parsed))
        {
            parsed = fallback;
        }

        return Math.Clamp(parsed, min, max);
    }

    private static int ParseOddInt(string? text, int fallback, int min, int max)
    {
        var parsed = ParseInt(text, fallback, min, max);
        return parsed % 2 == 0 ? parsed + 1 : parsed;
    }

    private static string BuildCameraName(string path, int sequence)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        if (string.IsNullOrWhiteSpace(name))
        {
            return $"Camera {sequence}";
        }

        return name;
    }

    internal enum CameraSourceKind
    {
        VideoFiles,
        UsbCamera
    }

    internal enum BakeSourceMode
    {
        Samples = 0,
        ImageFile = 1
    }

    [StructLayout(LayoutKind.Auto)]
    internal readonly record struct UsbCameraSource(int CameraIndex, VideoCaptureAPIs Api);

    internal readonly record struct CameraProfile(
        string Id,
        string DisplayName,
        bool IsVisible,
        bool IsIncludedInVisionPipeline,
        bool DebugViewEnabled,
        CameraSourceKind SourceKind,
        List<string> VideoPaths,
        UsbCameraSource? UsbCamera)
    {
        public string PrimaryVideoPath => VideoPaths.Count > 0 ? VideoPaths[0] : string.Empty;

        public bool IsUsbCamera => SourceKind == CameraSourceKind.UsbCamera && UsbCamera is not null;

        public bool CanOpenBakedMask => SourceKind == CameraSourceKind.VideoFiles && !string.IsNullOrWhiteSpace(PrimaryVideoPath);

        public string CurrentSourceLabel => IsUsbCamera && UsbCamera is { } usbCamera
            ? $"USB camera {usbCamera.CameraIndex}"
            : (string.IsNullOrWhiteSpace(PrimaryVideoPath) ? DisplayName : Path.GetFileName(PrimaryVideoPath));

        public static CameraProfile CreateVideo(string id, string displayName, List<string> videoPaths)
            => new(id, displayName, true, true, false, CameraSourceKind.VideoFiles, videoPaths, null);

        public static CameraProfile CreateUsb(string id, string displayName, int cameraIndex, VideoCaptureAPIs api)
            => new(id, displayName, true, true, false, CameraSourceKind.UsbCamera, new List<string>(), new UsbCameraSource(cameraIndex, api));
    }

    internal readonly record struct RuntimeProcessingSettings(
        int SampleCount,
        int Threshold,
        int MotionArea,
        int ColorMinPixels,
        int MorphKernelSize,
        int ProcessMaxWidth,
        BakeSourceMode BakeSourceMode,
        string BakeImagePath,
        IReadOnlyList<ColorCalibrationProfile> ColorCalibrations)
    {
        public static RuntimeProcessingSettings Default => new(20, 100, 220, 40, 3, 640, BakeSourceMode.Samples, string.Empty, CreateDefaultColorCalibrations());
    }

    private readonly record struct CameraZoneComboItem(string CameraZoneId, string CameraZoneName)
    {
        public override string ToString() => CameraZoneName;
    }

    private readonly record struct LayerTypeComboItem(string LayerTypeId, string DisplayName)
    {
        public override string ToString() => DisplayName;
    }

    private readonly record struct LayerListItem(string LayerId, string Name, string LayerTypeId)
    {
        public override string ToString() => $"{Name} ({LayerTypeId})";
    }

    private readonly record struct RegionListItem(string RegionId, string Name, int? Code, string CellsText)
    {
        public override string ToString() => Code is null ? Name : $"{Name} [{Code}]";
    }

    private sealed class CameraTileFeedConsumer(CancellationTokenSource cts, Task task) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            cts.Cancel();
            try
            {
                await task;
            }
            catch
            {
                // Ignore preview loop shutdown errors.
            }
            finally
            {
                cts.Dispose();
            }
        }
    }

    private sealed class EmptyAsyncDisposable : IAsyncDisposable
    {
        public static EmptyAsyncDisposable Instance { get; } = new();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private void InitializeRegionServices()
    {
        var persistence = new ObjectTracker.UI.Desktop.Region.Implementation.RegionPersistence(
            System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ObjectTracker",
                "regions.json"));

        regionRegistry = new ObjectTracker.UI.Desktop.Region.Implementation.RegionRegistry(persistence);

        regionManagerService = new ObjectTracker.UI.Desktop.Region.Implementation.RegionManagerService(
            regionRegistry,
            persistence,
            (owner, regionId) => BuildGridEditorDialogAsync(owner, regionId));
    }

    private async Task<ObjectTracker.UI.Desktop.Region.Implementation.GridEditorDialog?> BuildGridEditorDialogAsync(Avalonia.Controls.Window owner, Guid? regionId)
    {
        var camera = GetSelectedCamera();
        if (camera is null)
            return null;

        if (!cameraZoneIdentityService.TryGetCameraZoneForSource(camera.Value.Id, out var zone))
            return null;

        var currentCells = regionId.HasValue
            ? regionRegistry.GetById(regionId.Value)?.Cells ?? new List<ObjectTracker.UI.Desktop.Region.Model.GridCell>().AsReadOnly()
            : new List<ObjectTracker.UI.Desktop.Region.Model.GridCell>().AsReadOnly();

        var regionName = regionId.HasValue
            ? regionRegistry.GetById(regionId.Value)?.Name ?? "Edit Region"
            : "New Region";

        Func<Task<byte[]?>> frameLoader = () => LoadCameraFrameAsync(camera.Value);

        return new ObjectTracker.UI.Desktop.Region.Implementation.GridEditorDialog(
            appSettings.GridColumns,
            appSettings.GridRows,
            currentCells,
            regionName,
            frameLoader);
    }

    private async Task<byte[]?> LoadCameraFrameAsync(CameraProfile camera)
    {
        try
        {
            if (camera.SourceKind == CameraSourceKind.VideoFiles && !string.IsNullOrWhiteSpace(camera.PrimaryVideoPath))
            {
                using var capture = new VideoCapture(camera.PrimaryVideoPath);
                if (capture.IsOpened())
                {
                    using var mat = new Mat();
                    if (capture.Read(mat) && !mat.Empty())
                    {
                        Cv2.ImEncode(".jpg", mat, out var jpegBytes, new[] { (int)ImwriteFlags.JpegQuality, 80 });
                        return jpegBytes;
                    }
                }
            }
        }
        catch
        {
            // Silently fail - no frame is better than crashing
        }

        return null;
    }

    private async void RegionsWorkspaceButtonOnClick(object? sender, RoutedEventArgs e)
    {
        await TryNavigateWorkspaceAsync(Workspace.Regions);

        var camera = GetSelectedCamera();
        if (camera is null || !cameraZoneIdentityService.TryGetCameraZoneForSource(camera.Value.Id, out var zone))
        {
            RegionsListBox.ItemsSource = new List<string> { "No camera zone selected." };
            return;
        }

        var zoneId = new ObjectTracker.UI.Desktop.Region.Model.CameraZoneId(zone.CameraZoneId);
        var regions = regionManagerService.GetRegionsForZone(zoneId);
        RegionsListBox.ItemsSource = regions.Select(r => $"{r.Name} ({r.Type}, {r.Cells.Count} cells)").ToList();
    }

    private async void CreateRegionButtonOnClick(object? sender, RoutedEventArgs e)
    {
        var camera = GetSelectedCamera();
        if (camera is null || !cameraZoneIdentityService.TryGetCameraZoneForSource(camera.Value.Id, out var zone))
        {
            SetStatus("Status: select a camera first.");
            return;
        }

        var regionName = RegionNameTextBox.Text?.Trim() ?? "New Region";
        var selectedTypeItem = RegionTypeComboBox.SelectedItem as ComboBoxItem;
        var typeValue = int.TryParse(selectedTypeItem?.Tag?.ToString(), out var type) ? type : 0;
        var regionType = (ObjectTracker.UI.Desktop.Region.Model.RegionType)typeValue;

        var zoneId = new ObjectTracker.UI.Desktop.Region.Model.CameraZoneId(zone.CameraZoneId);

        await regionManagerService.OpenGridEditorAsync(zoneId, this, null);

        sessionAuditLogger.AppendEvent(
            SessionAuditLogger.EventRegionCreated,
            $"Region '{regionName}' created.",
            ("Zone", zone.CameraZoneId),
            ("Type", regionType.ToString()));

        SetStatus($"Status: region '{regionName}' created.");
    }

    private async void EditRegionButtonOnClick(object? sender, RoutedEventArgs e)
    {
        if (RegionsListBox.SelectedItem is not string selectedRegionText)
            return;

        var camera = GetSelectedCamera();
        if (camera is null || !cameraZoneIdentityService.TryGetCameraZoneForSource(camera.Value.Id, out var zone))
            return;

        var zoneId = new ObjectTracker.UI.Desktop.Region.Model.CameraZoneId(zone.CameraZoneId);
        var regions = regionManagerService.GetRegionsForZone(zoneId);
        var regionName = selectedRegionText.Split(' ')[0];
        var region = regions.FirstOrDefault(r => r.Name == regionName);

        if (region.Id == Guid.Empty)
            return;

        await regionManagerService.OpenGridEditorAsync(zoneId, this, region.Id);

        sessionAuditLogger.AppendEvent(
            SessionAuditLogger.EventRegionUpdated,
            $"Region '{region.Name}' edited.",
            ("Zone", zone.CameraZoneId));

        SetStatus($"Status: region '{region.Name}' edited.");
    }

    private async void DeleteRegionButtonOnClick(object? sender, RoutedEventArgs e)
    {
        if (RegionsListBox.SelectedItem is not string selectedRegionText)
            return;

        var camera = GetSelectedCamera();
        if (camera is null || !cameraZoneIdentityService.TryGetCameraZoneForSource(camera.Value.Id, out var zone))
            return;

        var zoneId = new ObjectTracker.UI.Desktop.Region.Model.CameraZoneId(zone.CameraZoneId);
        var regions = regionManagerService.GetRegionsForZone(zoneId);
        var regionName = selectedRegionText.Split(' ')[0];
        var region = regions.FirstOrDefault(r => r.Name == regionName);

        if (region.Id == Guid.Empty)
            return;

        var confirmed = await ConfirmDestructiveActionAsync("Delete Region", $"Are you sure you want to delete '{region.Name}'?");
        if (!confirmed)
            return;

        var deleted = regionManagerService.DeleteRegion(region.Id);
        if (deleted)
        {
            sessionAuditLogger.AppendEvent(
                SessionAuditLogger.EventRegionDeleted,
                $"Region '{region.Name}' deleted.",
                ("Zone", zone.CameraZoneId));

            SetStatus($"Status: region '{region.Name}' deleted.");
        }
    }

    private void RegionsListBoxOnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (RegionsListBox.SelectedItem is not string selectedRegionText)
        {
            RegionDetailNameText.Text = "No region selected";
            RegionDetailTypeInfoText.Text = "";
            RegionDetailCellCountText.Text = "";
            return;
        }

        var camera = GetSelectedCamera();
        if (camera is null || !cameraZoneIdentityService.TryGetCameraZoneForSource(camera.Value.Id, out var zone))
            return;

        var zoneId = new ObjectTracker.UI.Desktop.Region.Model.CameraZoneId(zone.CameraZoneId);
        var regions = regionManagerService.GetRegionsForZone(zoneId);
        var regionName = selectedRegionText.Split(' ')[0];
        var region = regions.FirstOrDefault(r => r.Name == regionName);

        if (region.Id == Guid.Empty)
        {
            RegionDetailNameText.Text = "No region selected";
            RegionDetailTypeInfoText.Text = "";
            RegionDetailCellCountText.Text = "";
        }
        else
        {
            RegionDetailNameText.Text = region.Name;
            RegionDetailTypeInfoText.Text = $"Type: {region.Type}";
            RegionDetailCellCountText.Text = $"Cells: {region.Cells.Count}";
        }
    }
}
