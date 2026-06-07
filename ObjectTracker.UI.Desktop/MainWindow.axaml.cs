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
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using FluentAvalonia.UI.Windowing;
using ObjectTracker.Core.Domain;
using ObjectTracker.Core.Ports;
using ObjectTracker.UI.Desktop.Enums;
using ObjectTracker.Vision;
using ObjectTracker.Vision.Source;
using VideoCapture = OpenCvSharp.VideoCapture;
using VideoCaptureAPIs = OpenCvSharp.VideoCaptureAPIs;
using VideoCaptureProperties = OpenCvSharp.VideoCaptureProperties;

namespace ObjectTracker.UI.Desktop;

public partial class MainWindow : AppWindow, IOutputPort
{

    public readonly record struct WorkspaceVisibility(bool CameraVisible, bool LayersVisible, bool SettingsVisible, bool CalibrationVisible = false);

    public readonly record struct BottomStatusSnapshot(
        string VisionPipeline,
        string AmbiguityAlert,
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

    public readonly record struct FileCameraSource(
        string CameraId,
        string DisplayName,
        string VideoPath,
        bool LoopVideo);

    public readonly record struct FileCameraSourceProjection(IReadOnlyList<FileCameraSource> Sources);

    public readonly record struct CameraWorkspaceTile(string CameraId, string DisplayName, int Index, CameraRenderMode RenderMode);

    public readonly record struct CameraTileFrameRoute(string CameraId, CameraTileFrameSource FrameSource);

    public readonly record struct CameraTileFrameRouting(IReadOnlyList<CameraTileFrameRoute> Routes);

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

    public static WorkspaceVisibility BuildWorkspaceVisibility(Workspace workspace)
    {
        return workspace switch
        {
            Workspace.Camera => new WorkspaceVisibility(true, false, false, false),
            Workspace.Layers => new WorkspaceVisibility(false, true, false, false),
            Workspace.Calibration => new WorkspaceVisibility(false, false, false, true),
            Workspace.Settings => new WorkspaceVisibility(false, false, true, false),
            _ => new WorkspaceVisibility(true, false, false, false)
        };
    }

    public static bool IsWorkspaceNavigationAllowedDuringAmbiguity()
    {
        return true;
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

    public static CameraGridProjection BuildCameraGridProjection(IReadOnlyList<CameraWorkspaceCamera> cameras)
    {
        return BuildCameraGridProjection(cameras, isVisionPipelineRunning: true);
    }

    public static CameraGridProjection BuildCameraGridProjection(IReadOnlyList<CameraWorkspaceCamera> cameras, bool isVisionPipelineRunning)
    {
        var visible = cameras.Where(camera => camera.IsVisible).ToList();
        var (rows, columns) = ComputeCameraGridDimensions(visible.Count);
        var tiles = visible
            .Select((camera, index) => new CameraWorkspaceTile(
                camera.CameraId,
                camera.DisplayName,
                index,
                GetCameraRenderMode(camera.IsIncludedInVisionPipeline, camera.DebugViewEnabled, isVisionPipelineRunning)))
            .ToList();

        return new CameraGridProjection(visible.Count, rows, columns, tiles);
    }

    public static FileCameraSourceProjection BuildFileCameraSourceProjection(IReadOnlyList<FileCameraSource> sources)
    {
        return new FileCameraSourceProjection(sources.ToList());
    }

    public static CameraRenderMode GetCameraRenderMode(bool isIncludedInVisionPipeline, bool debugViewEnabled)
    {
        return GetCameraRenderMode(isIncludedInVisionPipeline, debugViewEnabled, isVisionPipelineRunning: true);
    }

    public static CameraRenderMode GetCameraRenderMode(bool isIncludedInVisionPipeline, bool debugViewEnabled, bool isVisionPipelineRunning)
    {
        if (!isIncludedInVisionPipeline || !isVisionPipelineRunning)
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

    public static CameraTileFrameRouting BuildCameraTileFrameRouting(CameraGridProjection projection)
    {
        var routes = projection.Tiles
            .Select(tile => new CameraTileFrameRoute(tile.CameraId, GetCameraTileFrameSource(tile.RenderMode)))
            .ToList();

        return new CameraTileFrameRouting(routes);
    }

    private static CameraTileFrameSource GetCameraTileFrameSource(CameraRenderMode renderMode)
    {
        return renderMode switch
        {
            CameraRenderMode.DebugView => CameraTileFrameSource.PipelineSnapshotDebugFrames,
            CameraRenderMode.LiveAnnotated => CameraTileFrameSource.PipelineSnapshotAnnotatedFrame,
            _ => CameraTileFrameSource.RawCameraSourceFeed
        };
    }

    public static SelectionMode GetCameraListSelectionMode()
    {
        return SelectionMode.Single;
    }

    public static CameraDestructiveActionsState BuildCameraDestructiveActionsState(
        bool isVisionPipelineRunning,
        bool isAmbiguityActive,
        bool hasSelectedCamera,
        int cameraCount)
    {
        var runtimeBlocked = isVisionPipelineRunning || isAmbiguityActive;
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

    public static LayerTypeUsageProjection BuildLayerTypeUsageProjection(
        string layerTypeId,
        IReadOnlyCollection<CameraZoneLayer> cameraZoneLayers,
        IReadOnlyCollection<CameraZoneDefinition> cameraZones,
        IReadOnlyCollection<CameraZoneBinding> cameraZoneBindings,
        IReadOnlyDictionary<string, string> cameraDisplayNamesBySourceId)
    {
        var zoneNameById = cameraZones.ToDictionary(zone => zone.CameraZoneId, zone => zone.Name, StringComparer.OrdinalIgnoreCase);
        var cameraNameByZoneId = cameraZoneBindings
            .Where(binding => cameraDisplayNamesBySourceId.ContainsKey(binding.SourceId))
            .GroupBy(binding => binding.CameraZoneId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => cameraDisplayNamesBySourceId[group.First().SourceId],
                StringComparer.OrdinalIgnoreCase);

        var items = cameraZoneLayers
            .Where(layer => string.Equals(layer.LayerTypeId, layerTypeId, StringComparison.OrdinalIgnoreCase))
            .Select(layer => new LayerTypeUsageItem(
                cameraNameByZoneId.TryGetValue(layer.CameraZoneId, out var cameraName) ? cameraName : "Unbound camera source",
                zoneNameById.TryGetValue(layer.CameraZoneId, out var zoneName) ? zoneName : layer.CameraZoneId,
                layer.Name,
                layer.Regions.Count))
            .OrderBy(item => item.CameraDisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.LayerName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new LayerTypeUsageProjection(items);
    }

    public static LayerTypeDeleteState BuildLayerTypeDeleteState(string layerTypeId, LayerTypeUsageProjection usage)
    {
        if (usage.Items.Count == 0)
        {
            return new LayerTypeDeleteState(true, $"Layer Type {layerTypeId} can be deleted.");
        }

        var dependencies = string.Join(", ", usage.Items.Select(item => $"{item.CameraDisplayName}: {item.LayerName}"));
        return new LayerTypeDeleteState(false, $"Layer Type {layerTypeId} is in use and cannot be deleted. Remove Camera Layer Regions first: {dependencies}.");
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
            SettingsField.MissingFrameBehavior => "applies immediately",
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

    public static BottomStatusSnapshot BuildBottomStatusSnapshot(bool isVisionPipelineRunning, bool isAmbiguityActive, bool hasPendingVisionPipelineRestart)
    {
        return new BottomStatusSnapshot(
            VisionPipeline: isVisionPipelineRunning ? "Vision Pipeline: running" : "Vision Pipeline: stopped",
            AmbiguityAlert: isAmbiguityActive ? "Ambiguity Alert: active" : "Ambiguity Alert: clear",
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
    private readonly SessionCalibrationService sessionCalibration = new();
    private readonly CameraSettingsStore cameraSettingsStore = new();
    private readonly FileCameraSourceStore fileCameraSourceStore = new();
    private readonly UsbCaptureSettingsStore usbCaptureSettingsStore = new();
    private readonly CameraZoneBindingStore cameraZoneBindingStore = new();
    private readonly LayerTypeSettingsStore layerTypeSettingsStore = new();
    private readonly CameraZoneLayerRepository cameraZoneLayerRepository = new();
    private readonly AppSettingsStore appSettingsStore = new();
    private readonly UsbCameraOwnerManager usbCameraOwnerManager = new(new OpenCvUsbCaptureBackend());
    private readonly FileCameraSourceFeedManager fileCameraSourceFeedManager = new(new OpenCvFileCameraSourcePlaybackBackend());
    private readonly CameraTileRawFeedConsumer cameraTileRawFeedConsumer = new();
    private readonly CameraTilePipelineSnapshotRenderer cameraTilePipelineSnapshotRenderer = new();
    private readonly UsbCaptureSettingsService usbCaptureSettingsService;
    private readonly CameraZoneLayerEditorService cameraZoneLayerEditorService = new();
    private readonly EffectiveZoneCompositionService effectiveZoneCompositionService = new();
    private readonly SessionAuditLogger sessionAuditLogger = new();
    private readonly CameraZoneIdentityService cameraZoneIdentityService;
    private LayerTypeCatalogService layerTypeCatalogService;
    private AppSettings appSettings;
    private AppSettings draftAppSettings;
    private List<CameraZoneLayer> cameraZoneLayers = new();

    private CancellationTokenSource? runCts;
    private readonly CameraTileFeedCoordinator cameraTileFeedCoordinator;
    private readonly Dictionary<string, CameraProfile> cameraTileFeedCamerasById = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Image> cameraTileImagesById = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, CameraRenderMode> cameraTileRenderModesById = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DebugTileImageSet> cameraTileDebugImagesById = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> pendingUsbCaptureSettingsCameraSourceIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> activeVisionPipelineCameraSourceIds = new(StringComparer.OrdinalIgnoreCase);
    private Task? runTask;
    private IPipelineController? activeVisionPipeline;
    private int selectedCameraIndex = -1;
    private string selectedCalibrationColor = "red";
    private bool ambiguityActive;
    private string ambiguityMessage = "Ambiguity detected. Resolve before automatic processing continues.";
    private bool applyingCameraZoneUi;
    private bool applyingCameraVisibilityUi;
    private bool applyingCameraInclusionUi;
    private bool applyingCameraDebugViewUi;
    private bool applyingVideoLoopUi;
    private bool applyingUsbCaptureSettingsUi;
    private bool gridEditorVisible;
    private bool hasPendingVisionPipelineRestart;

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

        foreach (var source in fileCameraSourceStore.Load())
        {
            cameras.Add(CameraProfile.CreateVideo(source.CameraId, source.DisplayName, source.VideoPath, source.LoopVideo));
            if (!cameraSettings.ContainsKey(source.CameraId))
            {
                cameraSettings[source.CameraId] = RuntimeProcessingSettings.Default;
            }
        }

        if (cameras.Count > 0)
        {
            selectedCameraIndex = 0;
        }

        usbCaptureSettingsService = new UsbCaptureSettingsService(usbCaptureSettingsStore.Load());

        var cameraZoneSnapshot = cameraZoneBindingStore.Load();
        cameraZoneIdentityService = new CameraZoneIdentityService(cameraZoneSnapshot.Zones, cameraZoneSnapshot.Bindings);
        layerTypeCatalogService = layerTypeSettingsStore.Load();
        appSettings = appSettingsStore.Load();
        draftAppSettings = appSettings;
        PromptSettingsNavigationDecisionAsync = ShowSettingsNavigationGuardDialogAsync;
        cameraZoneLayers = cameraZoneLayerRepository.Load().ToList();
        ConfirmDestructiveActionAsync = ShowDestructiveConfirmationDialogAsync;
        cameraTileFeedCoordinator = new CameraTileFeedCoordinator(StartCameraTileFeedConsumer);
        InitializeUsbCaptureSettingsUi();

        HookEvents();
        SetActiveWorkspace(Workspace.Camera);
        SetRunState(isRunning: false);
        RefreshSettingsWorkspaceUi();
        RefreshCameraUi();
        RefreshLayerTypeUi();
        AppendLog($"Loaded {layerTypeCatalogService.GetOrderedByPrecedence().Count} layer type definitions.");
        AppendLog("Application initialized.");
        UpdateBottomStatusBar();
    }

    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        await StopCameraTilePreviewAsync();
        await StopProcessingAsync();
        await fileCameraSourceFeedManager.StopAllAsync(CancellationToken.None);
        await usbCameraOwnerManager.StopAllAsync(CancellationToken.None);
        PersistCameraSettings();
        PersistFileCameraSources();
        sessionAuditLogger.Dispose();
        base.OnClosing(e);
    }

    private void HookEvents()
    {
        CameraWorkspaceControl.AddCameraRequested += (_, _) => AddCamerasButtonOnClick(null, new RoutedEventArgs());
        CameraWorkspaceControl.RemoveCameraRequested += (_, _) => RemoveCameraButtonOnClick(null, new RoutedEventArgs());
        DeleteSelectedCameraMenuItem.Click += RemoveCameraButtonOnClick;
        MoveCameraUpButton.Click += MoveCameraUpButtonOnClick;
        MoveCameraDownButton.Click += MoveCameraDownButtonOnClick;
        ClearCamerasMenuItem.Click += ClearCamerasButtonOnClick;
        CameraWorkspaceControl.PreviousVideoRequested += (_, _) => PreviousCameraButtonOnClick(null, new RoutedEventArgs());
        CameraWorkspaceControl.NextVideoRequested += (_, _) => NextCameraButtonOnClick(null, new RoutedEventArgs());
        OpenBakedMaskButton.Click += OpenBakedMaskButtonOnClick;
        CameraSourceListBox.SelectionChanged += CameraSelectionChanged;
        CameraZoneComboBox.SelectionChanged += CameraZoneComboBoxOnSelectionChanged;
        CameraVisibilityCheckBox.IsCheckedChanged += CameraVisibilityCheckBoxOnChanged;
        VisionPipelineInclusionCheckBox.IsCheckedChanged += VisionPipelineInclusionCheckBoxOnChanged;
        CameraDebugViewCheckBox.IsCheckedChanged += CameraDebugViewCheckBoxOnChanged;
        RestartUsbCameraSourceButton.Click += RestartUsbCameraSourceButtonOnClick;
        UsbResolutionComboBox.SelectionChanged += UsbCaptureSettingsControlOnChanged;
        UsbTargetFpsComboBox.SelectionChanged += UsbCaptureSettingsControlOnChanged;
        ApplyUsbCaptureSettingsButton.Click += ApplyUsbCaptureSettingsButtonOnClick;
        RevertUsbCaptureSettingsButton.Click += RevertUsbCaptureSettingsButtonOnClick;
        CameraWorkspaceControl.ToggleGridEditorRequested += (_, _) => ToggleGridEditorButtonOnClick(null, new RoutedEventArgs());
        AddLayerButton.Click += AddLayerButtonOnClick;
        DeleteLayerButton.Click += DeleteLayerButtonOnClick;
        LayersListBox.SelectionChanged += LayersListBoxOnSelectionChanged;
        RegionsListBox.SelectionChanged += RegionsListBoxOnSelectionChanged;
        SaveRegionButton.Click += SaveRegionButtonOnClick;
        DeleteRegionButton.Click += DeleteRegionButtonOnClick;
        CameraWorkspaceControl.RefreshCompositionPreviewRequested += (_, _) => RefreshCompositionPreviewButtonOnClick(null, new RoutedEventArgs());
        CalibrationWorkspaceMenuItem.Click += CalibrationWorkspaceButtonOnClick;
        BakeSourceComboBox.SelectionChanged += BakeSourceComboBoxOnSelectionChanged;
        SelectBakeImageButton.Click += SelectBakeImageButtonOnClick;
        ClearBakeImageButton.Click += ClearBakeImageButtonOnClick;
        LoopVideoCheckBox.IsCheckedChanged += LoopVideoCheckBoxOnChanged;
        CameraWorkspaceControl.MarkAmbiguityRequested += (_, _) => MarkAmbiguityButtonOnClick(null, new RoutedEventArgs());
        CameraWorkspaceControl.ResolveRemovedRequested += (_, _) => ResolveRemovedButtonOnClick(null, new RoutedEventArgs());
        CameraWorkspaceControl.ResolveRelinkRequested += (_, _) => ResolveRelinkButtonOnClick(null, new RoutedEventArgs());
        CameraWorkspaceControl.ResolveFalseRequested += (_, _) => ResolveFalseButtonOnClick(null, new RoutedEventArgs());
        CameraWorkspaceControl.ResolveOtherRequested += (_, _) => ResolveOtherButtonOnClick(null, new RoutedEventArgs());
        OpenCameraWorkspaceMenuItem.Click += CameraWorkspaceButtonOnClick;
        LayersWorkspaceMenuItem.Click += LayersWorkspaceButtonOnClick;
        CalibrationWorkspaceMenuItem.Click += CalibrationWorkspaceButtonOnClick;
        SettingsWorkspaceMenuItem.Click += SettingsWorkspaceButtonOnClick;
        StartVisionPipelineMenuItem.Click += StartVisionPipelineMenuItemOnClick;
        StopVisionPipelineMenuItem.Click += StopVisionPipelineMenuItemOnClick;

        SettingsWorkspaceControl.SaveRequested += (_, _) => SaveSettingsButtonOnClick(null, new RoutedEventArgs());
        SettingsWorkspaceControl.DiscardRequested += (_, _) => DiscardSettingsButtonOnClick(null, new RoutedEventArgs());
        SettingsWorkspaceControl.DraftChanged += (_, _) => SettingsDraftTextBoxOnTextChanged(null, null);

        LayerWorkspaceControl.AddLayerTypeRequested += (_, _) => AddGlobalLayerTypeButtonOnClick(null, new RoutedEventArgs());
        LayerWorkspaceControl.DeleteLayerTypeRequested += (_, _) => DeleteGlobalLayerTypeButtonOnClick(null, new RoutedEventArgs());
        LayerWorkspaceControl.LayerTypeSelectionChanged += (_, _) => RefreshSelectedGlobalLayerTypeUsage();

        SampleCountTextBox.LostFocus += RuntimeSettingControlOnLostFocus;
        ThresholdTextBox.LostFocus += RuntimeSettingControlOnLostFocus;
        MotionAreaTextBox.LostFocus += RuntimeSettingControlOnLostFocus;
        ColorMinPixelsTextBox.LostFocus += RuntimeSettingControlOnLostFocus;
        MorphKernelSizeTextBox.LostFocus += RuntimeSettingControlOnLostFocus;
        ProcessWidthTextBox.LostFocus += RuntimeSettingControlOnLostFocus;

        CalibrationColorComboBox.SelectionChanged += CalibrationColorComboBoxOnSelectionChanged;
        CalibrationColorMinPicker.ColorChanged += (_, _) => UpdateSelectedColorCalibrationFromUi(logChange: false);
        CalibrationColorMaxPicker.ColorChanged += (_, _) => UpdateSelectedColorCalibrationFromUi(logChange: false);

        UpdateAmbiguityUi();
    }

    private async void CameraWorkspaceButtonOnClick(object? sender, RoutedEventArgs e)
    {
        await TryNavigateWorkspaceAsync(Workspace.Camera);
    }

    private async void LayersWorkspaceButtonOnClick(object? sender, RoutedEventArgs e)
    {
        await TryNavigateWorkspaceAsync(Workspace.Layers);
    }

    private async void CalibrationWorkspaceButtonOnClick(object? sender, RoutedEventArgs e)
    {
        await TryNavigateWorkspaceAsync(Workspace.Calibration);
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
        CameraWorkspaceControl.IsVisible = visibility.CameraVisible;
        LayerWorkspaceControl.IsVisible = visibility.LayersVisible;
        ColorCalibrationWorkspaceControl.IsVisible = visibility.CalibrationVisible;
        SettingsWorkspaceControl.IsVisible = visibility.SettingsVisible;
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

    private void SaveSettingsButtonOnClick(object? sender, RoutedEventArgs? e)
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

    private void DiscardSettingsButtonOnClick(object? sender, RoutedEventArgs? e)
    {
        draftAppSettings = appSettings;
        RefreshSettingsWorkspaceUi();
        SetStatus("Status: settings changes discarded.");
    }

    private void SettingsDraftTextBoxOnTextChanged(object? sender, TextChangedEventArgs? e)
    {
        UpdateDraftAppSettingsFromUi();
        RefreshSettingsDraftStatusUi();
    }

    private void SettingsMissingFrameBehaviorComboBoxOnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        UpdateDraftAppSettingsFromUi();
        RefreshSettingsDraftStatusUi();
    }

    private void UpdateDraftAppSettingsFromUi()
    {
        draftAppSettings = SettingsWorkspaceControl.ReadDraftSettings(appSettings);
    }

    private void RefreshSettingsWorkspaceUi()
    {
        SettingsWorkspaceControl.RefreshSettingsUi(appSettings, draftAppSettings, hasPendingVisionPipelineRestart, GetSettingsApplyPolicyLabel);
    }

    private void RefreshSettingsDraftStatusUi()
    {
        SettingsWorkspaceControl.RefreshDraftStatusUi(appSettings, draftAppSettings, hasPendingVisionPipelineRestart);
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
                cameras.Add(CameraProfile.CreateVideo(cameraId, displayName, path, loopVideo: true));
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
        PersistFileCameraSources();
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
        PersistFileCameraSources();
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
        PersistFileCameraSources();
        PersistCameraZones();
        RefreshCameraUi();

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
        await fileCameraSourceFeedManager.StopAllAsync(CancellationToken.None);
        await usbCameraOwnerManager.StopAllAsync(CancellationToken.None);

        PersistCameraSettings();
        PersistFileCameraSources();
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
        var index = CameraSourceListBox.SelectedIndex;
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
            CameraWorkspaceControl.SetCurrentVideo(BuildCurrentSourceText(camera.Value));
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
            applyingVideoLoopUi = true;
            LoopVideoCheckBox.IsChecked = camera.Value.LoopVideo;
            applyingVideoLoopUi = false;
            LoopVideoCheckBox.IsVisible = !camera.Value.IsUsbCamera;
            RefreshSelectedUsbCameraSourceStatusUi(camera.Value);
            RefreshUsbCaptureSettingsUi(camera.Value);
            UpdateCameraZoneSelectionUi(camera.Value.Id);
            RefreshLayerEditorUiForSelectedCamera();
        }

        if (runTask is not null && index >= 0 && camera is { } selected)
        {
            sessionAuditLogger.AppendEvent(
                SessionAuditLogger.EventCameraSwitch,
                "Selected Camera Source changed while Vision Pipeline continued running.",
                ("cameraId", selected.Id),
                ("cameraName", selected.DisplayName),
                ("requestedIndex", index.ToString()));
        }
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

        PersistFileCameraSources();
        RefreshCameraUi();
    }

    private async Task StartVisionPipelineFromUiAsync()
    {
        if (runTask is not null)
        {
            return;
        }

        if (ambiguityActive)
        {
            SetStatus("Status: ambiguity is active. Resolve alert before starting automatic processing.");
            return;
        }

        if (GetCameraCount() == 0)
        {
            SetStatus("Status: add at least one camera.");
            return;
        }

        UpdateSelectedCameraSettingsFromUi(logChange: false);

        var includedCameras = GetIncludedVisionPipelineCameras();
        if (includedCameras.Count == 0)
        {
            SetStatus("Status: include at least one Camera Source in the Vision Pipeline.");
            return;
        }

        var runStopwatch = Stopwatch.StartNew();
        var stopReason = "completed";

        runCts = new CancellationTokenSource();
        var token = runCts.Token;
        sessionAuditLogger.StartSession();
        var selectedCamera = GetSelectedCamera();
        sessionAuditLogger.AppendEvent(
            SessionAuditLogger.EventRunStart,
            "Processing run started.",
            ("cameraCount", GetCameraCount().ToString()),
            ("includedCameraCount", includedCameras.Count.ToString()),
            ("loopVideos", selectedCamera?.LoopVideo.ToString() ?? string.Empty),
            ("startCameraId", selectedCamera?.Id ?? string.Empty),
            ("startCameraName", selectedCamera?.DisplayName ?? string.Empty));

        if (!string.IsNullOrWhiteSpace(sessionAuditLogger.CurrentFilePath))
        {
            SetStatus($"Status: session log active at {sessionAuditLogger.CurrentFilePath}");
        }

        hasPendingVisionPipelineRestart = false;
        SetRunState(isRunning: true);
        StartBakeForAllCameras(token);
        var controller = CreateVisionPipelineController(includedCameras);
        activeVisionPipeline = controller;
        lock (activeVisionPipelineCameraSourceIds)
        {
            activeVisionPipelineCameraSourceIds.Clear();
            foreach (var camera in includedCameras)
            {
                activeVisionPipelineCameraSourceIds.Add(camera.Id);
            }
        }

        runTask = Task.Run(() => RunVisionPipelineControllerAsync(controller, includedCameras, token), token);

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
            activeVisionPipeline = null;
            lock (activeVisionPipelineCameraSourceIds)
            {
                activeVisionPipelineCameraSourceIds.Clear();
            }
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

    private List<CameraProfile> GetIncludedVisionPipelineCameras()
    {
        lock (cameraSync)
        {
            return cameras
                .Where(camera => camera.IsIncludedInVisionPipeline)
                .ToList();
        }
    }

    private IPipelineController CreateVisionPipelineController(IReadOnlyList<CameraProfile> includedCameras)
    {
        var configuredSources = includedCameras
            .Select(ToConfiguredCameraSource)
            .ToList();
        var detectorManager = new DetectorManager(new OpenCvColorDetector());
        return new PipelineController(
            new ConfiguredCameraSourceFrameSourceFactory(configuredSources, usbCameraOwnerManager, fileCameraSourceFeedManager),
            detectorManager,
            new SimpleTracker(),
            [this],
            new SystemClock());
    }

    private ConfiguredCameraSource ToConfiguredCameraSource(CameraProfile camera)
    {
        if (camera.IsUsbCamera && camera.UsbCamera is { } usb)
        {
            var key = new UsbCameraKey(usb.CameraIndex, usb.Api.ToString().ToUpperInvariant());
            return ConfiguredCameraSource.Usb(camera.Id, camera.DisplayName, key, ToUsbCaptureSettings(usbCaptureSettingsService.GetRequestedSettings(camera.Id)));
        }

        return ConfiguredCameraSource.File(camera.Id, camera.DisplayName, camera.VideoPath, camera.LoopVideo);
    }

    private async Task RunVisionPipelineControllerAsync(
        IPipelineController controller,
        IReadOnlyList<CameraProfile> includedCameras,
        CancellationToken cancellationToken)
    {
        foreach (var camera in includedCameras)
        {
            controller.SetVisionPipelineInclusion(camera.Id, included: true);
            controller.SetDebugViewEnabled(camera.Id, NormalizeDebugViewEnabled(camera.IsIncludedInVisionPipeline, camera.DebugViewEnabled));
            controller.SetVisualObservationSettings(camera.Id, await BuildVisualObservationSettingsAsync(camera, cancellationToken));
        }

        await controller.StartAsync(cancellationToken);

        try
        {
            await WaitForCancellationAsync(cancellationToken);
        }
        finally
        {
            await controller.StopAsync(CancellationToken.None);
        }
    }

    private static async Task WaitForCancellationAsync(CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        var cancellationSignal = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var registration = cancellationToken.UnsafeRegister(
            static state => ((TaskCompletionSource<object?>)state!).TrySetResult(null),
            cancellationSignal);
        await cancellationSignal.Task;
    }

    private static VisualObservationSettings ToVisualObservationSettings(RuntimeProcessingSettings settings) => new(
        settings.Threshold,
        settings.MotionArea,
        settings.ColorMinPixels,
        settings.MorphKernelSize,
        settings.ProcessMaxWidth,
        settings.ColorCalibrations,
        DebugViewEnabled: false);

    private async Task<VisualObservationSettings> BuildVisualObservationSettingsAsync(CameraProfile camera, CancellationToken cancellationToken)
    {
        var settings = GetSettingsForCamera(camera.Id);
        var visualObservationSettings = ToVisualObservationSettings(settings);
        if (camera.IsUsbCamera || string.IsNullOrWhiteSpace(camera.PrimaryVideoPath))
        {
            return visualObservationSettings;
        }

        var bakedBackgroundPath = await sessionCalibration.EnsureBakedBackgroundAsync(
            camera.PrimaryVideoPath,
            settings.SampleCount,
            settings.ProcessMaxWidth,
            GetBakeImagePath(settings),
            cancellationToken);
        return visualObservationSettings with
        {
            EncodedBackground = await File.ReadAllBytesAsync(bakedBackgroundPath, cancellationToken)
        };
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

            if (!string.IsNullOrWhiteSpace(camera.VideoPath))
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await engine.EnsureBakedBackgroundAsync(
                            camera.VideoPath,
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

    public async Task PublishSnapshotAsync(PipelineSnapshot snapshot, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        var routing = BuildCurrentCameraTileFrameRouting(isVisionPipelineRunning: true);
        await cameraTilePipelineSnapshotRenderer.RenderSnapshotAsync(
            snapshot,
            routing,
            frame => Dispatcher.UIThread.InvokeAsync(() => RenderPipelineSnapshotFrame(frame)).GetTask(),
            debugFrame => Dispatcher.UIThread.InvokeAsync(() => RenderPipelineSnapshotDebugFrame(debugFrame)).GetTask(),
            cancellationToken);
    }

    public Task PublishStatusAsync(string status, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.CompletedTask;
        }

        return Dispatcher.UIThread.InvokeAsync(() => SetStatus($"Status: {status}")).GetTask();
    }

    private CameraTileFrameRouting BuildCurrentCameraTileFrameRouting(bool isVisionPipelineRunning)
    {
        List<CameraProfile> snapshot;
        lock (cameraSync)
        {
            snapshot = cameras.ToList();
        }

        var projection = BuildCameraGridProjection(snapshot
            .Select(camera => new CameraWorkspaceCamera(
                camera.Id,
                camera.DisplayName,
                camera.IsVisible,
                camera.IsIncludedInVisionPipeline,
                NormalizeDebugViewEnabled(camera.IsIncludedInVisionPipeline, camera.DebugViewEnabled)))
            .ToList(),
            isVisionPipelineRunning);

        return BuildCameraTileFrameRouting(projection);
    }

    private void RenderPipelineSnapshotFrame(CameraTileRawFrameSnapshot frame)
    {
        if (!cameraTileImagesById.TryGetValue(frame.CameraId, out var target))
        {
            return;
        }

        UpdatePreviewImage(target, frame.EncodedJpeg);
    }

    private void RenderPipelineSnapshotDebugFrame(CameraTileDebugFrameSnapshot debugFrame)
    {
        if (!cameraTileDebugImagesById.TryGetValue(debugFrame.CameraId, out var debugImages))
        {
            return;
        }

        var target = debugFrame.Slot switch
        {
            CameraTileDebugFrameSlot.MovingObjectObservation => debugImages.Moving,
            CameraTileDebugFrameSlot.TrainObservation => debugImages.Color,
            CameraTileDebugFrameSlot.TrainTracking => debugImages.Motion,
            _ => debugImages.Background
        };

        UpdatePreviewImage(target, debugFrame.EncodedJpeg);
    }

    private static void UpdatePreviewImage(Image target, byte[] imageBytes)
    {
        using var ms = new MemoryStream(imageBytes);
        var bitmap = new Bitmap(ms);

        var previous = target.Source as Bitmap;
        target.IsVisible = true;
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

        CameraSourceListBox.ItemsSource = snapshot.ConvertAll(camera => camera.DisplayName);
        RefreshCameraWorkspaceTiles(snapshot);
        RefreshCameraZoneComboItems();
        ApplyCameraDestructiveActionsState(snapshot.Count);

        var canNavigate = snapshot.Count > 1;
        PreviousVideoButton.IsEnabled = canNavigate;
        NextVideoButton.IsEnabled = canNavigate;
        MoveCameraUpButton.IsEnabled = selectedCameraIndex > 0;
        MoveCameraDownButton.IsEnabled = selectedCameraIndex >= 0 && selectedCameraIndex < snapshot.Count - 1;

        if (snapshot.Count == 0)
        {
            CameraSourceListBox.SelectedIndex = -1;
            CameraWorkspaceControl.SetCurrentVideo("Current camera/source: -");
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
            LoopVideoCheckBox.IsVisible = false;
            UsbCameraSourceStatusText.Text = "USB Camera Source: -";
            RestartUsbCameraSourceButton.IsVisible = false;
            RestartUsbCameraSourceButton.IsEnabled = false;
            UsbCaptureSettingsPanel.IsVisible = false;
            CameraZoneComboBox.SelectedIndex = -1;
            LayersListBox.ItemsSource = null;
            RegionsListBox.ItemsSource = null;
            return;
        }

        selectedCameraIndex = Math.Clamp(selectedCameraIndex, 0, snapshot.Count - 1);
        CameraSourceListBox.SelectedIndex = selectedCameraIndex;

        var selected = snapshot[selectedCameraIndex];
        ApplySettingsToUi(GetSettingsForCamera(selected.Id));
        CameraWorkspaceControl.SetCurrentVideo(BuildCurrentSourceText(selected));
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
        applyingVideoLoopUi = true;
        LoopVideoCheckBox.IsChecked = selected.LoopVideo;
        applyingVideoLoopUi = false;
        LoopVideoCheckBox.IsVisible = !selected.IsUsbCamera;
        RefreshSelectedUsbCameraSourceStatusUi(selected);
        RefreshUsbCaptureSettingsUi(selected);
        UpdateCameraZoneSelectionUi(selected.Id);
        RefreshLayerEditorUiForSelectedCamera();
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
            .ToList(),
            isVisionPipelineRunning: runTask is not null);

        var viewState = BuildCameraTileViewState(projection);
        ApplyCameraTileViewState(viewState);
        StartCameraTilePreview(orderedCameras, projection);
    }

    private void StartCameraTilePreview(IReadOnlyList<CameraProfile> orderedCameras, CameraGridProjection projection)
    {
        var rawFeedIds = BuildCameraTileFrameRouting(projection)
            .Routes
            .Where(route => route.FrameSource == CameraTileFrameSource.RawCameraSourceFeed)
            .Select(route => route.CameraId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var feedCameras = orderedCameras
            .Where(camera => rawFeedIds.Contains(camera.Id))
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
                var feed = new UsbCameraTileRawFrameFeed(lease);
                var previousVersion = 0L;
                while (!cancellationToken.IsCancellationRequested)
                {
                    previousVersion = await cameraTileRawFeedConsumer.RenderNextFrameAsync(
                        feed,
                        previousVersion,
                        frame =>
                        {
                            RenderEncodedRawFrameToTile(target, frame.EncodedJpeg);
                            return Task.CompletedTask;
                        },
                        cancellationToken);

                    await DelayIgnoringCancellationAsync(PreviewIntervalMs, cancellationToken);
                }

                return;
            }

            if (string.IsNullOrWhiteSpace(camera.VideoPath))
            {
                return;
            }

            var fileKey = new FileCameraSourceKey(camera.Id, camera.VideoPath);
            await using var fileLease = await fileCameraSourceFeedManager.AcquireAsync(
                fileKey,
                new FileCameraSourcePlaybackSettings(camera.LoopVideo),
                cancellationToken);
            var fileFeed = new FileCameraTileRawFrameFeed(fileLease);
            var filePreviousVersion = 0L;
            while (!cancellationToken.IsCancellationRequested)
            {
                filePreviousVersion = await cameraTileRawFeedConsumer.RenderNextFrameAsync(
                    fileFeed,
                    filePreviousVersion,
                    frame =>
                    {
                        RenderEncodedRawFrameToTile(target, frame.EncodedJpeg);
                        return Task.CompletedTask;
                    },
                    cancellationToken);

                await DelayIgnoringCancellationAsync(PreviewIntervalMs, cancellationToken);
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

    private void RenderEncodedRawFrameToTile(Image target, byte[] encodedJpeg)
    {
        using var stream = new MemoryStream(encodedJpeg);
        var bitmap = new Bitmap(stream);
        Dispatcher.UIThread.Post(() => UpdatePreviewBitmap(target, bitmap), DispatcherPriority.Background);
    }

    private static void UpdatePreviewBitmap(Image target, Bitmap bitmap)
    {
        var previous = target.Source as Bitmap;
        target.IsVisible = true;
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

            var displayView = CameraTileDisplayProjection.Build(
                hasCurrentFrame: image.Source is not null,
                hasLastFrame: image.Source is not null,
                missingFrameBehavior: appSettings.MissingFrameBehavior,
                frameSource: GetCameraTileFrameSource(viewState.RenderModes[i]));
            image.IsVisible = displayView.FrameDisplay != CameraTileFrameDisplay.BlackFrame;

            panel.Children.Add(image);

            if (displayView.FrameDisplay == CameraTileFrameDisplay.Placeholder)
            {
                panel.Children.Add(new Border
                {
                    Background = Avalonia.Media.Brush.Parse("#CC111820"),
                    Child = new TextBlock
                    {
                        Text = displayView.PlaceholderText,
                        Foreground = Avalonia.Media.Brush.Parse("#EAF4FF"),
                        FontWeight = Avalonia.Media.FontWeight.SemiBold,
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(12)
                    }
                });
            }

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

    private void LoopVideoCheckBoxOnChanged(object? sender, RoutedEventArgs e)
    {
        if (applyingVideoLoopUi)
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
            if (selected.IsUsbCamera)
            {
                return;
            }

            cameras[selectedCameraIndex] = selected with { LoopVideo = LoopVideoCheckBox.IsChecked == true };
        }

        PersistFileCameraSources();
        RefreshCameraUi();
    }

    private async void RestartUsbCameraSourceButtonOnClick(object? sender, RoutedEventArgs e)
    {
        var camera = GetSelectedCamera();
        if (camera is not { IsUsbCamera: true, UsbCamera: { } usb })
        {
            return;
        }

        if (IsActivelyProcessedByVisionPipeline(camera.Value.Id))
        {
            SetStatus("Status: stop Vision Pipeline to restart this camera source.");
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
            IsActivelyProcessedByVisionPipeline(camera.Value.Id),
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
            isActivelyProcessedByVisionPipeline: IsActivelyProcessedByVisionPipeline(camera.Id),
            status);
    }

    private bool IsActivelyProcessedByVisionPipeline(string cameraSourceId)
    {
        lock (activeVisionPipelineCameraSourceIds)
        {
            return activeVisionPipelineCameraSourceIds.Contains(cameraSourceId);
        }
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

        string? selectedCameraId = null;
        var isIncluded = VisionPipelineInclusionCheckBox.IsChecked == true;
        lock (cameraSync)
        {
            if (selectedCameraIndex < 0 || selectedCameraIndex >= cameras.Count)
            {
                return;
            }

            var selected = cameras[selectedCameraIndex];
            selectedCameraId = selected.Id;
            cameras[selectedCameraIndex] = selected with
            {
                IsIncludedInVisionPipeline = isIncluded,
                DebugViewEnabled = NormalizeDebugViewEnabled(isIncluded, selected.DebugViewEnabled)
            };
        }

        activeVisionPipeline?.SetVisionPipelineInclusion(selectedCameraId, isIncluded);
        lock (activeVisionPipelineCameraSourceIds)
        {
            if (isIncluded)
            {
                activeVisionPipelineCameraSourceIds.Add(selectedCameraId);
            }
            else
            {
                activeVisionPipelineCameraSourceIds.Remove(selectedCameraId);
            }
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

        string? selectedCameraId = null;
        var debugEnabled = CameraDebugViewCheckBox.IsChecked == true;
        lock (cameraSync)
        {
            if (selectedCameraIndex < 0 || selectedCameraIndex >= cameras.Count)
            {
                return;
            }

            var selected = cameras[selectedCameraIndex];
            selectedCameraId = selected.Id;
            cameras[selectedCameraIndex] = selected with
            {
                DebugViewEnabled = NormalizeDebugViewEnabled(selected.IsIncludedInVisionPipeline, debugEnabled)
            };
        }

        activeVisionPipeline?.SetDebugViewEnabled(selectedCameraId, debugEnabled);

        RefreshCameraUi();
    }

    private void ApplyCameraTileGridDimensions(int rows, int columns)
    {
        CameraTileGrid.Rows = Math.Max(1, rows);
        CameraTileGrid.Columns = Math.Max(1, columns);
    }

    private void ToggleGridEditorButtonOnClick(object? sender, RoutedEventArgs e)
    {
        gridEditorVisible = !gridEditorVisible;
        GridEditorPanel.IsVisible = gridEditorVisible;
        ToggleGridEditorButton.Content = gridEditorVisible
            ? "Hide Grid Region Editor"
            : "Open Grid Region Editor";
    }

    private void RefreshLayerTypeUi()
    {
        var orderedDefinitions = layerTypeCatalogService
            .GetOrderedByPrecedence()
            .ToList();

        LayerTypeComboBox.ItemsSource = orderedDefinitions
            .Select(definition => new LayerTypeComboItem(definition.LayerTypeId, definition.DisplayName))
            .ToList();
        LayerTypeComboBox.SelectedIndex = 0;

        var selectedLayerTypeId = LayerWorkspaceControl.SelectedLayerTypeId;
        var globalItems = orderedDefinitions
            .Select(definition => new GlobalLayerTypeListItem(
                definition.LayerTypeId,
                definition.DisplayName,
                definition.Precedence,
                definition.MergePolicy,
                definition.BehaviorClass))
            .ToList();
        LayerWorkspaceControl.SetLayerTypeItems(globalItems);
        var selectedIndex = string.IsNullOrWhiteSpace(selectedLayerTypeId)
            ? 0
            : globalItems.FindIndex(item => string.Equals(item.LayerTypeId, selectedLayerTypeId, StringComparison.OrdinalIgnoreCase));
        LayerWorkspaceControl.SetLayerTypeSelection(globalItems.Count == 0 ? -1 : Math.Max(0, selectedIndex));
        RefreshSelectedGlobalLayerTypeUsage();
    }

    private void RefreshSelectedGlobalLayerTypeUsage()
    {
        var selectedLayerTypeId = LayerWorkspaceControl.SelectedLayerTypeId;
        if (string.IsNullOrWhiteSpace(selectedLayerTypeId))
        {
            LayerWorkspaceControl.SetLayerTypeDraft(string.Empty, string.Empty, string.Empty);
            LayerWorkspaceControl.SetUsageItems(null);
            LayerWorkspaceControl.SetDeleteStatus("Select a Layer Type to inspect usage.", false);
            return;
        }

        var selected = layerTypeCatalogService.GetOrderedByPrecedence()
            .FirstOrDefault(definition => string.Equals(definition.LayerTypeId, selectedLayerTypeId, StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrWhiteSpace(selected.LayerTypeId))
        {
            LayerWorkspaceControl.SetLayerTypeDraft(string.Empty, string.Empty, string.Empty);
            LayerWorkspaceControl.SetUsageItems(null);
            LayerWorkspaceControl.SetDeleteStatus("Select a Layer Type to inspect usage.", false);
            return;
        }

        LayerWorkspaceControl.SetLayerTypeDraft(selected.LayerTypeId, selected.DisplayName, selected.Precedence.ToString());

        var usage = BuildLayerTypeUsageProjection(
            selected.LayerTypeId,
            cameraZoneLayers,
            cameraZoneIdentityService.CameraZones,
            cameraZoneIdentityService.SourceBindings,
            BuildCameraDisplayNamesBySourceId());
        LayerWorkspaceControl.SetUsageItems(usage.Items.Count == 0
            ? new[] { "No camera usage." }
            : usage.Items.Select(item => $"{item.CameraDisplayName} / {item.CameraZoneName}: {item.LayerName} ({item.RegionCount} regions)").ToList());

        var deleteState = BuildLayerTypeDeleteState(selected.LayerTypeId, usage);
        LayerWorkspaceControl.SetDeleteStatus(deleteState.Message, deleteState.CanDelete);
    }

    private Dictionary<string, string> BuildCameraDisplayNamesBySourceId()
    {
        lock (cameraSync)
        {
            return cameras.ToDictionary(camera => camera.Id, camera => camera.DisplayName, StringComparer.OrdinalIgnoreCase);
        }
    }

    private void RefreshLayerEditorUiForSelectedCamera()
    {
        var camera = GetSelectedCamera();
        if (camera is null || !cameraZoneIdentityService.TryGetCameraZoneForSource(camera.Value.Id, out var zone))
        {
            LayersListBox.ItemsSource = null;
            RegionsListBox.ItemsSource = null;
            return;
        }

        var layers = cameraZoneLayers
            .Where(layer => string.Equals(layer.CameraZoneId, zone.CameraZoneId, StringComparison.OrdinalIgnoreCase))
            .Select(layer => new LayerListItem(layer.LayerId, layer.Name, layer.LayerTypeId))
            .ToList();

        LayersListBox.ItemsSource = layers;
        if (layers.Count == 0)
        {
            LayersListBox.SelectedIndex = -1;
            RegionsListBox.ItemsSource = null;
            CompositionPreviewListBox.ItemsSource = null;
            return;
        }

        if (LayersListBox.SelectedItem is not LayerListItem selected || !layers.Any(item => item.LayerId == selected.LayerId))
        {
            LayersListBox.SelectedIndex = 0;
        }

        RefreshRegionsForSelectedLayer();
        RefreshCompositionPreview();
    }

    private void RefreshRegionsForSelectedLayer()
    {
        if (LayersListBox.SelectedItem is not LayerListItem selectedLayer)
        {
            RegionsListBox.ItemsSource = null;
            return;
        }

        var layer = cameraZoneLayers.FirstOrDefault(item => string.Equals(item.LayerId, selectedLayer.LayerId, StringComparison.OrdinalIgnoreCase));
        var regions = layer.Regions
            .Select(region => new RegionListItem(region.RegionId, region.Name, region.Code, CameraZoneLayerEditorService.ToCellsText(region.Cells)))
            .ToList();
        RegionsListBox.ItemsSource = regions;
        RegionsListBox.SelectedIndex = regions.Count > 0 ? 0 : -1;
    }

    private void SetRunState(bool isRunning)
    {
        var menuState = BuildVisionPipelineMenuState(isRunning);
        StartVisionPipelineMenuItem.IsEnabled = menuState.StartEnabled;
        StopVisionPipelineMenuItem.IsEnabled = menuState.StopEnabled;
        AddVideosButton.IsEnabled = !isRunning && !ambiguityActive;
        RemoveSelectedButton.IsEnabled = !isRunning && !ambiguityActive;
        LoopVideoCheckBox.IsEnabled = !isRunning && !ambiguityActive;
        MarkAmbiguityButton.IsEnabled = !ambiguityActive;

        ApplyCameraDestructiveActionsState(GetCameraCount());

        UpdateBottomStatusBar();
    }

    private void ApplyCameraDestructiveActionsState(int cameraCount)
    {
        var selectedIndex = CameraSourceListBox.SelectedIndex;
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
            isAmbiguityActive: ambiguityActive,
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

            var candidate = CameraSourceListBox.SelectedIndex;
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

    private async void MarkAmbiguityButtonOnClick(object? sender, RoutedEventArgs e)
    {
        if (ambiguityActive)
        {
            return;
        }

        if (runTask is not null)
        {
            await StopProcessingAsync();
        }

        RaiseAmbiguity("Operator marked identity ambiguity and halted automatic processing.");
    }

    private void ResolveRemovedButtonOnClick(object? sender, RoutedEventArgs e)
    {
        ResolveAmbiguity("removed-from-track");
    }

    private void ResolveRelinkButtonOnClick(object? sender, RoutedEventArgs e)
    {
        ResolveAmbiguity("relinked-to-correct-id");
    }

    private void ResolveFalseButtonOnClick(object? sender, RoutedEventArgs e)
    {
        ResolveAmbiguity("false-ambiguity");
    }

    private void ResolveOtherButtonOnClick(object? sender, RoutedEventArgs e)
    {
        ResolveAmbiguity("other");
    }

    private void RaiseAmbiguity(string reason)
    {
        ambiguityActive = true;
        ambiguityMessage = reason;
        UpdateAmbiguityUi();
        SetRunState(isRunning: runTask is not null);
        SetStatus("Status: ambiguity raised. Automatic processing blocked until operator resolution.");

        sessionAuditLogger.AppendEvent(
            SessionAuditLogger.EventAmbiguityRaised,
            reason,
            ("cameraId", GetSelectedCamera()?.Id ?? string.Empty),
            ("cameraName", GetSelectedCamera()?.DisplayName ?? string.Empty));
    }

    private void ResolveAmbiguity(string outcome)
    {
        if (!ambiguityActive)
        {
            return;
        }

        ambiguityActive = false;
        var previousMessage = ambiguityMessage;
        ambiguityMessage = "Ambiguity detected. Resolve before automatic processing continues.";
        UpdateAmbiguityUi();
        SetRunState(isRunning: runTask is not null);
        SetStatus($"Status: ambiguity resolved ({outcome}). You can resume automatic processing.");

        sessionAuditLogger.AppendEvent(
            SessionAuditLogger.EventAmbiguityResolved,
            $"Resolved ambiguity: {outcome}.",
            ("outcome", outcome),
            ("originalReason", previousMessage),
            ("cameraId", GetSelectedCamera()?.Id ?? string.Empty),
            ("cameraName", GetSelectedCamera()?.DisplayName ?? string.Empty));
    }

    private void UpdateAmbiguityUi()
    {
        CameraWorkspaceControl.SetAmbiguityBanner(ambiguityActive, ambiguityMessage);
        UpdateBottomStatusBar();
    }

    private void UpdateBottomStatusBar()
    {
        var snapshot = BuildBottomStatusSnapshot(runTask is not null, ambiguityActive, hasPendingVisionPipelineRestart);
        BottomVisionPipelineStateText.Text = snapshot.VisionPipeline;
        BottomAmbiguityStateText.Text = snapshot.AmbiguityAlert;
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

    private void PersistCameraSettings()
    {
        Dictionary<string, RuntimeProcessingSettings> snapshot;
        lock (settingsSync)
        {
            snapshot = cameraSettings.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        }

        cameraSettingsStore.Save(snapshot);
    }

    private void PersistFileCameraSources()
    {
        List<FileCameraSource> snapshot;
        lock (cameraSync)
        {
            snapshot = cameras
                .Where(camera => !camera.IsUsbCamera)
                .Select(camera => new FileCameraSource(camera.Id, camera.DisplayName, camera.VideoPath, camera.LoopVideo))
                .ToList();
        }

        fileCameraSourceStore.Save(snapshot);
    }

    private void PersistCameraZones()
    {
        cameraZoneBindingStore.Save(cameraZoneIdentityService.CameraZones, cameraZoneIdentityService.SourceBindings);
    }

    private void RefreshCameraZoneComboItems()
    {
        applyingCameraZoneUi = true;
        try
        {
            CameraZoneComboBox.ItemsSource = cameraZoneIdentityService
                .GetCameraZonesOrderedByName()
                .Select(zone => new CameraZoneComboItem(zone.CameraZoneId, zone.Name))
                .ToList();
        }
        finally
        {
            applyingCameraZoneUi = false;
        }
    }

    private void UpdateCameraZoneSelectionUi(string sourceId)
    {
        applyingCameraZoneUi = true;
        try
        {
            if (!cameraZoneIdentityService.TryGetCameraZoneForSource(sourceId, out var zone))
            {
                CameraZoneComboBox.SelectedIndex = -1;
                return;
            }

            if (CameraZoneComboBox.ItemsSource is not IEnumerable<CameraZoneComboItem> items)
            {
                CameraZoneComboBox.SelectedIndex = -1;
                return;
            }

            var selected = items.FirstOrDefault(item => string.Equals(item.CameraZoneId, zone.CameraZoneId, StringComparison.OrdinalIgnoreCase));
            CameraZoneComboBox.SelectedItem = selected;
        }
        finally
        {
            applyingCameraZoneUi = false;
        }
    }

    private void CameraZoneComboBoxOnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (applyingCameraZoneUi)
        {
            return;
        }

        var camera = GetSelectedCamera();
        if (camera is null)
        {
            return;
        }

        if (CameraZoneComboBox.SelectedItem is not CameraZoneComboItem selected)
        {
            return;
        }

        cameraZoneIdentityService.AssignSourceToZone(camera.Value.Id, selected.CameraZoneId);
        PersistCameraZones();
        RefreshLayerEditorUiForSelectedCamera();
        SetStatus($"Status: {camera.Value.DisplayName} rebound to Camera Zone {selected.CameraZoneName}.");
    }

    private void AddLayerButtonOnClick(object? sender, RoutedEventArgs e)
    {
        var camera = GetSelectedCamera();
        if (camera is null)
        {
            SetStatus("Status: select a camera first.");
            return;
        }

        if (!cameraZoneIdentityService.TryGetCameraZoneForSource(camera.Value.Id, out var zone))
        {
            SetStatus("Status: no Camera Zone binding for selected camera.");
            return;
        }

        if (LayerTypeComboBox.SelectedItem is not LayerTypeComboItem layerType)
        {
            SetStatus("Status: select a layer type first.");
            return;
        }

        cameraZoneLayers = cameraZoneLayerEditorService
            .AddLayer(cameraZoneLayers, zone.CameraZoneId, layerType.LayerTypeId, $"{layerType.DisplayName} Layer")
            .ToList();
        cameraZoneLayerRepository.Save(cameraZoneLayers);
        RefreshLayerEditorUiForSelectedCamera();
        SetStatus($"Status: added {layerType.DisplayName} layer in {zone.Name}.");
    }

    private void AddGlobalLayerTypeButtonOnClick(object? sender, RoutedEventArgs e)
    {
        var (idText, nameText, precedenceText) = LayerWorkspaceControl.ReadLayerTypeDraft();
        if (!int.TryParse(precedenceText, out var precedence))
        {
            SetStatus("Status: enter a numeric Layer Type precedence.");
            return;
        }

        var result = layerTypeCatalogService.TryAddLayerType(new LayerTypeDefinition(
            idText,
            nameText,
            precedence,
            LayerMergePolicy.MergeForEffectiveMask,
            LayerTypeBehaviorClass.Informational));
        if (!result.Added)
        {
            LayerWorkspaceControl.SetDeleteStatus(result.Warning, false);
            SetStatus($"Status: {result.Warning}");
            return;
        }

        layerTypeCatalogService = result.Catalog;
        layerTypeSettingsStore.Save(layerTypeCatalogService.GetOrderedByPrecedence());
        RefreshLayerTypeUi();
        SetStatus($"Status: added Layer Type {nameText}.");
    }

    private void DeleteGlobalLayerTypeButtonOnClick(object? sender, RoutedEventArgs e)
    {
        var selectedLayerTypeId = LayerWorkspaceControl.SelectedLayerTypeId;
        var selectedDisplayName = LayerWorkspaceControl.SelectedLayerTypeDisplayName;
        if (string.IsNullOrWhiteSpace(selectedLayerTypeId))
        {
            return;
        }

        var usage = BuildLayerTypeUsageProjection(
            selectedLayerTypeId,
            cameraZoneLayers,
            cameraZoneIdentityService.CameraZones,
            cameraZoneIdentityService.SourceBindings,
            BuildCameraDisplayNamesBySourceId());
        var deleteState = BuildLayerTypeDeleteState(selectedLayerTypeId, usage);
        if (!deleteState.CanDelete)
        {
            SetStatus($"Status: {deleteState.Message}");
            RefreshSelectedGlobalLayerTypeUsage();
            return;
        }

        layerTypeCatalogService = layerTypeCatalogService.RemoveLayerType(selectedLayerTypeId);
        layerTypeSettingsStore.Save(layerTypeCatalogService.GetOrderedByPrecedence());
        RefreshLayerTypeUi();
        SetStatus($"Status: deleted Layer Type {selectedDisplayName}.");
    }

    private void DeleteLayerButtonOnClick(object? sender, RoutedEventArgs e)
    {
        if (LayersListBox.SelectedItem is not LayerListItem selectedLayer)
        {
            return;
        }

        cameraZoneLayers = cameraZoneLayerEditorService.RemoveLayer(cameraZoneLayers, selectedLayer.LayerId).ToList();
        cameraZoneLayerRepository.Save(cameraZoneLayers);
        RefreshLayerEditorUiForSelectedCamera();
        SetStatus($"Status: deleted layer {selectedLayer.Name}.");
    }

    private void LayersListBoxOnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        RefreshRegionsForSelectedLayer();
    }

    private void RegionsListBoxOnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (RegionsListBox.SelectedItem is not RegionListItem region)
        {
            RegionNameTextBox.Text = string.Empty;
            RegionCodeTextBox.Text = string.Empty;
            RegionCellsTextBox.Text = string.Empty;
            return;
        }

        RegionNameTextBox.Text = region.Name;
        RegionCodeTextBox.Text = region.Code?.ToString() ?? string.Empty;
        RegionCellsTextBox.Text = region.CellsText;
    }

    private void SaveRegionButtonOnClick(object? sender, RoutedEventArgs e)
    {
        if (LayersListBox.SelectedItem is not LayerListItem selectedLayer)
        {
            SetStatus("Status: select a layer first.");
            return;
        }

        var regionId = RegionsListBox.SelectedItem is RegionListItem selectedRegionItem ? selectedRegionItem.RegionId : null;
        int? code = int.TryParse(RegionCodeTextBox.Text, out var parsedCode) ? parsedCode : null;

        try
        {
            cameraZoneLayers = cameraZoneLayerEditorService.UpsertRegion(
                    cameraZoneLayers,
                    selectedLayer.LayerId,
                    regionId,
                    RegionNameTextBox.Text ?? string.Empty,
                    code,
                    RegionCellsTextBox.Text ?? string.Empty,
                    appSettings)
                .ToList();
        }
        catch (InvalidOperationException ex)
        {
            SetStatus($"Status: {ex.Message}");
            return;
        }

        cameraZoneLayerRepository.Save(cameraZoneLayers);
        RefreshLayerEditorUiForSelectedCamera();
        SetStatus("Status: region saved.");
    }

    private void DeleteRegionButtonOnClick(object? sender, RoutedEventArgs e)
    {
        if (LayersListBox.SelectedItem is not LayerListItem selectedLayer
            || RegionsListBox.SelectedItem is not RegionListItem selectedRegion)
        {
            return;
        }

        cameraZoneLayers = cameraZoneLayerEditorService
            .RemoveRegion(cameraZoneLayers, selectedLayer.LayerId, selectedRegion.RegionId)
            .ToList();
        cameraZoneLayerRepository.Save(cameraZoneLayers);
        RefreshLayerEditorUiForSelectedCamera();
        SetStatus("Status: region deleted.");
    }

    private void RefreshCompositionPreviewButtonOnClick(object? sender, RoutedEventArgs e)
    {
        RefreshCompositionPreview();
        SetStatus("Status: composition preview refreshed.");
    }

    private void RefreshCompositionPreview()
    {
        var camera = GetSelectedCamera();
        if (camera is null || !cameraZoneIdentityService.TryGetCameraZoneForSource(camera.Value.Id, out var zone))
        {
            CompositionPreviewListBox.ItemsSource = null;
            return;
        }

        var composed = effectiveZoneCompositionService.Compose(zone.CameraZoneId, cameraZoneLayers, layerTypeCatalogService);
        CompositionPreviewListBox.ItemsSource = composed.Select(item => item.DisplayText).ToList();
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
            current = Math.Clamp(CameraSourceListBox.SelectedIndex, 0, count - 1);
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
        CameraSourceListBox.SelectedIndex = target;

        if (runTask is not null)
        {
            SetStatus($"Status: selected camera #{target + 1}; Vision Pipeline continues processing included Camera Sources.");
            if (TryGetCamera(target, out var requestedCamera))
            {
                sessionAuditLogger.AppendEvent(
                    SessionAuditLogger.EventCameraSwitch,
                    "Selected Camera Source changed while Vision Pipeline continued running.",
                    ("cameraId", requestedCamera.Id),
                    ("cameraName", requestedCamera.DisplayName),
                    ("requestedIndex", target.ToString()));
            }

            return;
        }

        if (TryGetCamera(target, out var camera))
        {
            ApplySettingsToUi(GetSettingsForCamera(camera.Id));
            CameraWorkspaceControl.SetCurrentVideo(BuildCurrentSourceText(camera));
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
        CameraWorkspaceControl.SetStatus(text);
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

        var minimumColor = CalibrationColorMinPicker.Color;
        var maximumColor = CalibrationColorMaxPicker.Color;
        var (hueLower, hueUpper, saturationLower, saturationUpper, valueLower, valueUpper) = BuildCalibrationBoundsFromColor(minimumColor, maximumColor);
        var updated = NormalizeColorCalibration(new ColorCalibrationProfile(
            selectedColor,
            hueLower,
            hueUpper,
            saturationLower,
            saturationUpper,
            valueLower,
            valueUpper));

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

        CalibrationColorMinPicker.Color = BuildRepresentativeColor(profile, useLowerBound: true);
        CalibrationColorMaxPicker.Color = BuildRepresentativeColor(profile, useLowerBound: false);
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

    private static (int HueLower, int HueUpper, int SaturationLower, int SaturationUpper, int ValueLower, int ValueUpper) BuildCalibrationBoundsFromColor(Color minimumColor, Color maximumColor)
    {
        var (hMin, sMin, vMin) = ToHsv(minimumColor);
        var (hMax, sMax, vMax) = ToHsv(maximumColor);

        return (
            Math.Clamp((int)Math.Round(hMin / 2.0), 0, 180),
            Math.Clamp((int)Math.Round(hMax / 2.0), 0, 180),
            Math.Clamp((int)Math.Round(sMin * 255.0), 0, 255),
            Math.Clamp((int)Math.Round(sMax * 255.0), 0, 255),
            Math.Clamp((int)Math.Round(vMin * 255.0), 0, 255),
            Math.Clamp((int)Math.Round(vMax * 255.0), 0, 255));
    }

    private static Color BuildRepresentativeColor(ColorCalibrationProfile profile, bool useLowerBound)
    {
        var hue = useLowerBound ? profile.HueLower : profile.HueUpper;
        var saturation = useLowerBound ? profile.SaturationLower : profile.SaturationUpper;
        var value = useLowerBound ? profile.ValueLower : profile.ValueUpper;

        return FromHsv(hue * 2.0, saturation / 255.0, value / 255.0);
    }

    private static (double H, double S, double V) ToHsv(Color color)
    {
        var r = color.R / 255.0;
        var g = color.G / 255.0;
        var b = color.B / 255.0;

        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;

        var h = 0.0;
        if (delta > 0)
        {
            if (Math.Abs(max - r) < 1e-9)
            {
                h = 60.0 * ((g - b) / delta % 6.0);
            }
            else if (Math.Abs(max - g) < 1e-9)
            {
                h = 60.0 * ((b - r) / delta + 2.0);
            }
            else
            {
                h = 60.0 * ((r - g) / delta + 4.0);
            }
        }

        var s = max == 0.0 ? 0.0 : delta / max;
        var v = max;

        return (h, s, v);
    }

    private static Color FromHsv(double hueDegrees, double saturation, double value)
    {
        var c = value * saturation;
        var x = c * (1.0 - Math.Abs((hueDegrees / 60.0) % 2.0 - 1.0));
        var m = value - c;

        double r1, g1, b1;
        if (hueDegrees < 60.0)
        {
            r1 = c;
            g1 = x;
            b1 = 0.0;
        }
        else if (hueDegrees < 120.0)
        {
            r1 = x;
            g1 = c;
            b1 = 0.0;
        }
        else if (hueDegrees < 180.0)
        {
            r1 = 0.0;
            g1 = c;
            b1 = x;
        }
        else if (hueDegrees < 240.0)
        {
            r1 = 0.0;
            g1 = x;
            b1 = c;
        }
        else if (hueDegrees < 300.0)
        {
            r1 = x;
            g1 = 0.0;
            b1 = c;
        }
        else
        {
            r1 = c;
            g1 = 0.0;
            b1 = x;
        }

        return new Color(
            255,
            (byte)Math.Round((r1 + m) * 255.0),
            (byte)Math.Round((g1 + m) * 255.0),
            (byte)Math.Round((b1 + m) * 255.0));
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

    [StructLayout(LayoutKind.Auto)]
    private readonly record struct UsbCameraSource(int CameraIndex, VideoCaptureAPIs Api);

    private readonly record struct CameraProfile(
        string Id,
        string DisplayName,
        bool IsVisible,
        bool IsIncludedInVisionPipeline,
        bool DebugViewEnabled,
        CameraSourceKind SourceKind,
        string VideoPath,
        bool LoopVideo,
        UsbCameraSource? UsbCamera)
    {
        public string PrimaryVideoPath => VideoPath;

        public bool IsUsbCamera => SourceKind == CameraSourceKind.UsbCamera && UsbCamera is not null;

        public bool CanOpenBakedMask => SourceKind == CameraSourceKind.VideoFiles && !string.IsNullOrWhiteSpace(PrimaryVideoPath);

        public string CurrentSourceLabel => IsUsbCamera && UsbCamera is { } usbCamera
            ? $"USB camera {usbCamera.CameraIndex}"
            : (string.IsNullOrWhiteSpace(PrimaryVideoPath) ? DisplayName : Path.GetFileName(PrimaryVideoPath));

        public static CameraProfile CreateVideo(string id, string displayName, string videoPath, bool loopVideo)
            => new(id, displayName, true, true, false, CameraSourceKind.VideoFiles, videoPath, loopVideo, null);

        public static CameraProfile CreateUsb(string id, string displayName, int cameraIndex, VideoCaptureAPIs api)
            => new(id, displayName, true, true, false, CameraSourceKind.UsbCamera, string.Empty, false, new UsbCameraSource(cameraIndex, api));
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

    private readonly record struct GlobalLayerTypeListItem(
        string LayerTypeId,
        string DisplayName,
        int Precedence,
        LayerMergePolicy MergePolicy,
        LayerTypeBehaviorClass BehaviorClass)
    {
        public override string ToString() => $"{DisplayName} ({LayerTypeId}, {Precedence})";
    }

    private readonly record struct LayerListItem(string LayerId, string Name, string LayerTypeId)
    {
        public override string ToString() => $"{Name} ({LayerTypeId})";
    }

    private readonly record struct RegionListItem(string RegionId, string Name, int? Code, string CellsText)
    {
        public override string ToString() => Code is null ? Name : $"{Name} [{Code}]";
    }

    private sealed class UsbCameraTileRawFrameFeed(UsbCameraLease lease) : ICameraTileRawFrameFeed
    {
        public CameraTileRawFrameSnapshot? LatestFrame => ToRawFrame(lease.LatestFrame);

        public async Task<CameraTileRawFrameSnapshot?> WaitForNextFrameAsync(long previousVersion, TimeSpan timeout, CancellationToken cancellationToken)
        {
            return ToRawFrame(await lease.WaitForNextFrameAsync(previousVersion, timeout, cancellationToken));
        }

        private static CameraTileRawFrameSnapshot? ToRawFrame(UsbFrameSnapshot? snapshot)
        {
            return snapshot is { } frame
                ? new CameraTileRawFrameSnapshot(frame.SourceId, frame.FrameVersion, frame.Width, frame.Height, frame.EncodedJpeg)
                : null;
        }
    }

    private sealed class FileCameraTileRawFrameFeed(FileCameraSourceFeedLease lease) : ICameraTileRawFrameFeed
    {
        public CameraTileRawFrameSnapshot? LatestFrame => ToRawFrame(lease.LatestFrame);

        public async Task<CameraTileRawFrameSnapshot?> WaitForNextFrameAsync(long previousVersion, TimeSpan timeout, CancellationToken cancellationToken)
        {
            return ToRawFrame(await lease.WaitForNextFrameAsync(previousVersion, timeout, cancellationToken));
        }

        private static CameraTileRawFrameSnapshot? ToRawFrame(FileCameraSourceFrameSnapshot? snapshot)
        {
            return snapshot is { } frame
                ? new CameraTileRawFrameSnapshot(frame.SourceId, frame.FrameVersion, frame.Width, frame.Height, frame.EncodedJpeg)
                : null;
        }
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
}
