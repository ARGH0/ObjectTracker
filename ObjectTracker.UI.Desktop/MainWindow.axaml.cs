using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
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
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Windowing;
using Microsoft.Extensions.DependencyInjection;
using ObjectTracker.Core.Domain;
using ObjectTracker.UI.Desktop.Plc.Implementation;
using ObjectTracker.UI.Desktop.Plc.Model;
using ObjectTracker.UI.Desktop.Plc.Siemens;
using ObjectTracker.UI.Desktop.Region.Model;
using OpenCvSharp;

namespace ObjectTracker.UI.Desktop;

public partial class MainWindow : AppWindow
{
    public enum Workspace
    {
        Camera,
        Regions,
        Trains,
        Settings,
        Plc
    }

    public readonly record struct WorkspaceVisibility(bool CameraVisible, bool RegionsVisible, bool TrainsVisible, bool SettingsVisible, bool PlcVisible);

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

    public readonly record struct TrainEditorProjection(
        Guid TrainId,
        string Name,
        string PlcId,
        uint MinColor,
        uint MaxColor,
        string MaxWidth,
        string MaxHeight,
        string HueLower,
        string HueUpper,
        string SaturationLower,
        string SaturationUpper,
        string ValueLower,
        string ValueUpper);

    public enum DebugViewFrameType
    {
        MovingColor,
        ColorDetections
    }

    public readonly record struct CameraWorkspaceCamera(
        string CameraId,
        string DisplayName,
        bool IsVisible,
        bool IsIncludedInVisionPipeline,
        bool DebugViewEnabled,
        DebugViewFrameType DebugViewFrameType = DebugViewFrameType.MovingColor,
        bool ShowRegionsEnabled = false);

    public readonly record struct CameraWorkspaceTile(string CameraId, string DisplayName, int Index, FeedKind FeedKind, DebugViewFrameType DebugViewFrameType);

    public enum FeedKind
    {
        RawFeed,
        LiveAnnotated,
        DebugView
    }

    public static FeedKind GetFeedKind(bool isIncludedInVisionPipeline, bool debugViewEnabled)
    {
        if (!isIncludedInVisionPipeline)
        {
            return FeedKind.RawFeed;
        }
        return debugViewEnabled ? FeedKind.DebugView : FeedKind.LiveAnnotated;
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
        GridRows,
        AdaptiveBackgroundSampleCount,
        AdaptiveBackgroundUpdateIntervalFrames,
        PlcBaseUrl,
        PlcUser,
        PlcPassword
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
        IReadOnlyList<FeedKind> FeedKinds,
        IReadOnlyList<DebugViewFrameType> DebugViewFrameTypes,
        IReadOnlyList<bool> ShowRegionsEnabled);

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
            Workspace.Camera => new WorkspaceVisibility(true, false, false, false, false),
            Workspace.Regions => new WorkspaceVisibility(false, true, false, false, false),
            Workspace.Trains => new WorkspaceVisibility(false, false, true, false, false),
            Workspace.Settings => new WorkspaceVisibility(false, false, false, true, false),
            Workspace.Plc => new WorkspaceVisibility(false, false, false, false, true),
            _ => new WorkspaceVisibility(true, false, false, false, false)
        };
    }

    public static bool IsRuntimeLogVisibleForWorkspace(Workspace workspace)
    {
        return workspace == Workspace.Camera || workspace == Workspace.Plc;
    }

    public static VisionPipelineMenuState BuildVisionPipelineMenuState(bool isVisionPipelineRunning)
    {
        return isVisionPipelineRunning
            ? new VisionPipelineMenuState(StartEnabled: false, StopEnabled: true)
            : new VisionPipelineMenuState(StartEnabled: true, StopEnabled: false);
    }

    public static TrainEditorProjection BuildTrainEditorProjection(ConfiguredTrain train)
    {
        return new TrainEditorProjection(
            train.Id,
            train.Name,
            train.PlcId,
            train.MinColor,
            train.MaxColor,
            train.MaxWidth.ToString(CultureInfo.InvariantCulture),
            train.MaxHeight.ToString(CultureInfo.InvariantCulture),
            train.Calibration.HueLower.ToString(CultureInfo.InvariantCulture),
            train.Calibration.HueUpper.ToString(CultureInfo.InvariantCulture),
            train.Calibration.SaturationLower.ToString(CultureInfo.InvariantCulture),
            train.Calibration.SaturationUpper.ToString(CultureInfo.InvariantCulture),
            train.Calibration.ValueLower.ToString(CultureInfo.InvariantCulture),
            train.Calibration.ValueUpper.ToString(CultureInfo.InvariantCulture));
    }

    public static TrainEditorProjection ApplyTrainCalibrationPreset(string preset)
    {
        var train = TrainStore.CreateDefaultTrains()
            .FirstOrDefault(item => item.Name.StartsWith(preset, StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrWhiteSpace(train.Name))
        {
            train = TrainStore.CreateDefaultTrains()[0];
        }

        return BuildTrainEditorProjection(train);
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
            .Select((camera, index) =>
            {
                var feedKind = GetFeedKind(camera.IsIncludedInVisionPipeline, camera.DebugViewEnabled);
                return new CameraWorkspaceTile(
                    camera.CameraId,
                    camera.DisplayName,
                    index,
                    feedKind,
                    camera.DebugViewFrameType);
            })
            .ToList();

        return new CameraGridProjection(visible.Count, rows, columns, tiles);
    }

    public static IReadOnlyList<bool> BuildShowRegionsList(IReadOnlyList<CameraWorkspaceCamera> cameras)
    {
        return cameras
            .Where(camera => camera.IsVisible)
            .Select(camera => camera.ShowRegionsEnabled)
            .ToList();
    }

    public static bool NormalizeDebugViewEnabled(bool isIncludedInVisionPipeline, bool isVisionPipelineRunning, bool debugViewEnabled)
    {
        return isIncludedInVisionPipeline && isVisionPipelineRunning && debugViewEnabled;
    }

    internal static IReadOnlyList<CameraTileFeedRequest> BuildCameraTileFeedRequests(
        IReadOnlyList<CameraProfile> orderedCameras,
        CameraGridProjection projection,
        IReadOnlySet<string> availableTileImageIds,
        bool isVisionPipelineRunning)
    {
        var visibleIds = projection.Tiles.Select(tile => tile.CameraId).ToHashSet(StringComparer.OrdinalIgnoreCase);

        return orderedCameras
            .Where(camera => visibleIds.Contains(camera.Id))
            .Where(camera => !isVisionPipelineRunning || !camera.IsIncludedInVisionPipeline)
            .Where(camera => projection.Tiles.Any(tile =>
                string.Equals(tile.CameraId, camera.Id, StringComparison.OrdinalIgnoreCase) &&
                tile.FeedKind != FeedKind.DebugView))
            .Where(camera => availableTileImageIds.Contains(camera.Id))
            .Select(camera => new CameraTileFeedRequest(camera.Id, GetCameraTileFeedKind(camera.SourceKind)))
            .ToList();
    }

    private static CameraTileFeedKind GetCameraTileFeedKind(CameraSourceKind sourceKind)
    {
        return sourceKind switch
        {
            CameraSourceKind.UsbCamera => CameraTileFeedKind.UsbCamera,
            _ => CameraTileFeedKind.VideoFile
        };
    }

    public static string GetFeedKindBadge(FeedKind kind)
    {
        return kind switch
        {
            FeedKind.RawFeed => "RAW FEED",
            FeedKind.DebugView => "DEBUG VIEW",
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

    public static CameraTileViewState BuildCameraTileViewState(CameraGridProjection projection, IReadOnlyList<bool> showRegionsEnabled)
    {
        var titles = projection.Tiles
            .Select((tile, index) => $"{index + 1}. {tile.DisplayName} [{tile.FeedKind}]")
            .ToList();
        var ids = projection.Tiles.Select(tile => tile.CameraId).ToList();
        var feedKinds = projection.Tiles.Select(tile => tile.FeedKind).ToList();
        var debugViewFrameTypes = projection.Tiles.Select(tile => tile.DebugViewFrameType).ToList();
        return new CameraTileViewState(projection.Rows, projection.Columns, titles, ids, feedKinds, debugViewFrameTypes, showRegionsEnabled);
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
            SettingsField.AdaptiveBackgroundSampleCount => "requires Vision Pipeline restart",
            SettingsField.AdaptiveBackgroundUpdateIntervalFrames => "requires Vision Pipeline restart",
            SettingsField.PlcBaseUrl => "applies immediately",
            SettingsField.PlcUser => "applies immediately",
            SettingsField.PlcPassword => "applies immediately",
            _ => "applies immediately"
        };
    }

    public static SettingsSaveImpact BuildSettingsSaveImpact(AppSettings savedSettings, AppSettings draftSettings, bool isVisionPipelineRunning)
    {
        var requiresRestart = savedSettings.GridColumns != draftSettings.GridColumns
            || savedSettings.GridRows != draftSettings.GridRows
            || savedSettings.AdaptiveBackgroundSampleCount != draftSettings.AdaptiveBackgroundSampleCount
            || savedSettings.AdaptiveBackgroundUpdateIntervalFrames != draftSettings.AdaptiveBackgroundUpdateIntervalFrames;
        var pendingRestart = requiresRestart && isVisionPipelineRunning;
        return new SettingsSaveImpact(
            requiresRestart,
            pendingRestart,
            pendingRestart ? "Settings: saved, pending Vision Pipeline restart" : "Settings: saved");
    }

    public static bool HasPlcChanges(AppSettings a, AppSettings b)
    {
        return a.Plc.BaseUrl != b.Plc.BaseUrl
            || a.Plc.User != b.Plc.User
            || a.Plc.Password != b.Plc.Password;
    }

    public static BottomStatusSnapshot BuildBottomStatusSnapshot(bool isVisionPipelineRunning, bool hasPendingVisionPipelineRestart)
    {
        return new BottomStatusSnapshot(
            VisionPipeline: isVisionPipelineRunning ? "Vision Pipeline: running" : "Vision Pipeline: stopped",
            Calibration: "Calibration: unknown",
            PendingRestart: hasPendingVisionPipelineRestart ? "Pending restart: required" : "Pending restart: none");
    }

    public static List<string> BuildRegionListText(
        ObjectTracker.UI.Desktop.Region.Model.CameraZoneId? zoneId,
        string fallbackMessage,
        IEnumerable<ObjectTracker.UI.Desktop.Region.Model.RegionDefinition> regions)
    {
        if (zoneId == null)
            return new List<string> { fallbackMessage };

        var regionList = regions as ObjectTracker.UI.Desktop.Region.Model.RegionDefinition[] ?? regions.ToArray();
        if (regionList.Length == 0)
            return new List<string> { fallbackMessage };

        return regionList
            .Select(r => $"{r.Name}")
            .ToList();
    }

    public static List<string> BuildRegionListText(
        ObjectTracker.UI.Desktop.Region.Model.CameraZoneId? zoneId,
        string fallbackMessage)
    {
        return BuildRegionListText(zoneId, fallbackMessage, Array.Empty<ObjectTracker.UI.Desktop.Region.Model.RegionDefinition>());
    }

    private const int MaxLogEntries = 300;
    private const int MaxPlcLogEntries = 25;
    private const int PreviewIntervalMs = 33;

    private readonly Lock cameraSync = new();
    private readonly Lock settingsSync = new();
    private readonly List<CameraProfile> cameras = new();
    private readonly List<ConfiguredTrain> configuredTrains = new();
    private readonly Dictionary<string, RuntimeProcessingSettings> cameraSettings = new(StringComparer.OrdinalIgnoreCase);
    private readonly ObservableCollection<string> logEntries = new();
    private readonly ObservableCollection<string> plcLogEntries = new();

    private readonly BackgroundEstimationEngine engine = new();
    private readonly CameraSettingsStore cameraSettingsStore = new();
    private readonly TrainStore trainStore = new();
    private readonly CameraZoneBindingStore cameraZoneBindingStore = new();
    private readonly AppSettingsStore appSettingsStore = new();
    private readonly SessionAuditLogger sessionAuditLogger = new();
    private readonly CameraZoneIdentityService cameraZoneIdentityService;
    private ObjectTracker.UI.Desktop.Region.Contracts.IRegionManagerService regionManagerService;
    private ObjectTracker.UI.Desktop.Region.Contracts.IRegionRegistry regionRegistry;
    private ObjectTracker.UI.Desktop.Plc.Contracts.IPlcClient? plcClient;
    private ObjectTracker.UI.Desktop.Plc.Contracts.IPlcSessionManager? plcSessionManager;
    private AppSettings appSettings;
    private AppSettings draftAppSettings;

    private CancellationTokenSource? runCts;
    private readonly CameraTileFeedCoordinator cameraTileFeedCoordinator;
    private readonly Dictionary<string, CameraProfile> cameraTileFeedCamerasById = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Image> cameraTileImagesById = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, FeedKind> cameraTileFeedKindsById = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DebugTileImageSet> cameraTileDebugImagesById = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DebugViewFrameType> cameraTileDebugFrameTypesById = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, bool> cameraTileRegionsEnabledById = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, VideoFrameSnapshot> latestCameraFramesById = new(StringComparer.OrdinalIgnoreCase);
    private Task? runTask;
    private int selectedCameraIndex = -1;
    private int selectedTrainIndex = -1;
    private int requestedCameraIndex = -1;
    private bool applyingCameraVisibilityUi;
    private bool applyingCameraInclusionUi;
    private bool applyingCameraDebugViewUi;
    private bool applyingShowRegionsUi;
    private bool applyingTrainColorPickerUi;
    private bool hasPendingVisionPipelineRestart;
    private bool isVisionPipelineRunningForUi;
    private bool isCameraPanelOpen = true;
    private bool isCameraPanelPinned = true;
    private Workspace activeWorkspace = Workspace.Camera;
    internal Func<string, string, Task<bool>> ConfirmDestructiveActionAsync { get; set; }
    internal Func<Task<SettingsNavigationDecision>> PromptSettingsNavigationDecisionAsync { get; set; }
    private bool _isPlcConnected;

    private sealed record DebugTileImageSet(Image DebugPreview);

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

        configuredTrains.AddRange(trainStore.Load());

        var cameraZoneSnapshot = cameraZoneBindingStore.Load();
        cameraZoneIdentityService = new CameraZoneIdentityService(cameraZoneSnapshot.Zones, cameraZoneSnapshot.Bindings);
        InitializeRegionServices();
        appSettings = appSettingsStore.Load();
        draftAppSettings = appSettings;
        PromptSettingsNavigationDecisionAsync = ShowSettingsNavigationGuardDialogAsync;
        ConfirmDestructiveActionAsync = ShowDestructiveConfirmationDialogAsync;
        cameraTileFeedCoordinator = new CameraTileFeedCoordinator(StartCameraTileFeedConsumer);
        InitializePlcServices();

        HookEvents();
        SetActiveWorkspace(Workspace.Camera);
        InitializePlcWorkspaceUi();
        RefreshTrainWorkspaceUi();
        SetRunState(isRunning: false);
        ApplyCameraPanelLayout();
        RefreshSettingsWorkspaceUi();
        RefreshCameraUi();
        AppendLog("Application initialized.");
        UpdateBottomStatusBar();
    }

    private void InitializePlcServices()
    {
        try
        {
            var plcConfig = appSettings.Plc.ToClientConfig();
            var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
            services.AddSiemensPlcServices(plcConfig);
            var sp = services.BuildServiceProvider();
            plcClient = sp.GetRequiredService<ObjectTracker.UI.Desktop.Plc.Contracts.IPlcClient>();
            plcSessionManager = sp.GetRequiredService<ObjectTracker.UI.Desktop.Plc.Contracts.IPlcSessionManager>();
            AppendLog($"PLC services initialized (Siemens Webserver API): {plcClient.GetType().Name}.");
        }
        catch (Exception ex)
        {
            AppendLog($"PLC services init failed: {ex.Message}");
        }
    }

    private void InitializePlcWorkspaceUi()
    {
        PlcOperationLogText.Text = "No operations yet.";
        PlcConnectionStatusText.Text = "Not connected";
        PlcConnectButton.IsEnabled = plcClient is not null;
        PlcReadButton.IsEnabled = false;
        PlcWriteButton.IsEnabled = false;
    }

    private void AppendPlcLog(string message)
    {
        _ = Dispatcher.UIThread.InvokeAsync(() =>
        {
            AppendLog($"PLC: {message}");
            plcLogEntries.Add(message);
            while (plcLogEntries.Count > MaxPlcLogEntries)
            {
                plcLogEntries.RemoveAt(0);
            }

            PlcOperationLogText.Text = string.Join(Environment.NewLine, plcLogEntries);
        });
    }

    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        await StopCameraTilePreviewAsync();
        await StopProcessingAsync();
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
        DebugViewFrameTypeComboBox.SelectionChanged += DebugViewFrameTypeComboBoxOnSelectionChanged;
        ShowRegionsCheckBox.IsCheckedChanged += ShowRegionsCheckBoxOnChanged;
        BakeSourceComboBox.SelectionChanged += BakeSourceComboBoxOnSelectionChanged;
        SelectBakeImageButton.Click += SelectBakeImageButtonOnClick;
        ClearBakeImageButton.Click += ClearBakeImageButtonOnClick;
        OpenCameraWorkspaceMenuItem.Click += CameraWorkspaceButtonOnClick;
        RegionsWorkspaceMenuItem.Click += RegionsWorkspaceButtonOnClick;
        TrainsWorkspaceMenuItem.Click += TrainsWorkspaceButtonOnClick;
        SettingsWorkspaceMenuItem.Click += SettingsWorkspaceButtonOnClick;
        PlcWorkspaceMenuItem.Click += PlcWorkspaceButtonOnClick;
        TrainsListBox.SelectionChanged += TrainsListBoxOnSelectionChanged;
        AddTrainButton.Click += AddTrainButtonOnClick;
        DeleteTrainButton.Click += DeleteTrainButtonOnClick;
        SaveTrainButton.Click += SaveTrainButtonOnClick;
        TrainPresetRedButton.Click += (_, _) => ApplyTrainPresetToEditor("Red");
        TrainPresetGreenButton.Click += (_, _) => ApplyTrainPresetToEditor("Green");
        TrainPresetBlueButton.Click += (_, _) => ApplyTrainPresetToEditor("Blue");
        TrainPresetWhiteButton.Click += (_, _) => ApplyTrainPresetToEditor("White");
        TrainMinColorPicker.ColorChanged += TrainColorPickerOnColorChanged;
        TrainMaxColorPicker.ColorChanged += TrainColorPickerOnColorChanged;
        CreateRegionButton.Click += CreateRegionButtonOnClick;
        EditRegionButton.Click += EditRegionButtonOnClick;
        DeleteRegionButton.Click += DeleteRegionButtonOnClick;
        ExportRegionButton.Click += ExportRegionButtonOnClick;
        ImportRegionButton.Click += ImportRegionButtonOnClick;
        ExportCameraRegionsButton.Click += ExportCameraRegionsButtonOnClick;
        ImportCameraRegionsButton.Click += ImportCameraRegionsButtonOnClick;
        RegionsListBox.SelectionChanged += RegionsListBoxOnSelectionChanged;
        StartVisionPipelineMenuItem.Click += StartVisionPipelineMenuItemOnClick;
        StopVisionPipelineMenuItem.Click += StopVisionPipelineMenuItemOnClick;
        ToggleCameraPanelButton.Click += ToggleCameraPanelButtonOnClick;
        PinCameraPanelButton.Click += PinCameraPanelButtonOnClick;
        SaveSettingsButton.Click += SaveSettingsButtonOnClick;
        DiscardSettingsButton.Click += DiscardSettingsButtonOnClick;
        SettingsGridColumnsTextBox.TextChanged += SettingsDraftTextBoxOnTextChanged;
        SettingsGridRowsTextBox.TextChanged += SettingsDraftTextBoxOnTextChanged;
        SettingsAdaptiveBackgroundSampleCountTextBox.TextChanged += SettingsDraftTextBoxOnTextChanged;
        SettingsAdaptiveBackgroundUpdateIntervalFramesTextBox.TextChanged += SettingsDraftTextBoxOnTextChanged;
        SettingsPlcBaseUrlTextBox.TextChanged += SettingsDraftTextBoxOnTextChanged;
        SettingsPlcUserTextBox.TextChanged += SettingsDraftTextBoxOnTextChanged;
        SettingsPlcPasswordTextBox.TextChanged += SettingsDraftTextBoxOnTextChanged;

        PlcConnectButton.Click += PlcConnectButtonOnClick;
        PlcReadButton.Click += PlcReadButtonOnClick;
        PlcWriteButton.Click += PlcWriteButtonOnClick;
        PlcClearLogButton.Click += PlcClearLogButtonOnClick;

        SampleCountTextBox.LostFocus += RuntimeSettingControlOnLostFocus;
        ThresholdTextBox.LostFocus += RuntimeSettingControlOnLostFocus;
        MotionAreaTextBox.LostFocus += RuntimeSettingControlOnLostFocus;
        ColorMinPixelsTextBox.LostFocus += RuntimeSettingControlOnLostFocus;
        MorphKernelSizeTextBox.LostFocus += RuntimeSettingControlOnLostFocus;
        ProcessWidthTextBox.LostFocus += RuntimeSettingControlOnLostFocus;

    }

    private async void CameraWorkspaceButtonOnClick(object? sender, RoutedEventArgs e)
    {
        await TryNavigateWorkspaceAsync(Workspace.Camera);
    }

    private async void LayersWorkspaceButtonOnClick(object? sender, RoutedEventArgs e)
    {
        await TryNavigateWorkspaceAsync(Workspace.Regions);
    }

    private async void TrainsWorkspaceButtonOnClick(object? sender, RoutedEventArgs e)
    {
        await TryNavigateWorkspaceAsync(Workspace.Trains);
    }

    private async void SettingsWorkspaceButtonOnClick(object? sender, RoutedEventArgs e)
    {
        await TryNavigateWorkspaceAsync(Workspace.Settings);
    }

    private async void PlcWorkspaceButtonOnClick(object? sender, RoutedEventArgs e)
    {
        await TryNavigateWorkspaceAsync(Workspace.Plc);
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
        TrainsWorkspacePanel.IsVisible = visibility.TrainsVisible;
        SettingsWorkspacePanel.IsVisible = visibility.SettingsVisible;
        PlcWorkspacePanel.IsVisible = visibility.PlcVisible;
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
        var adaptiveBackgroundSampleCount = ParseInt(
            SettingsAdaptiveBackgroundSampleCountTextBox.Text,
            appSettings.AdaptiveBackgroundSampleCount,
            AppSettings.MinAdaptiveBackgroundSampleCount,
            int.MaxValue);
        var adaptiveBackgroundUpdateIntervalFrames = ParseInt(
            SettingsAdaptiveBackgroundUpdateIntervalFramesTextBox.Text,
            appSettings.AdaptiveBackgroundUpdateIntervalFrames,
            AppSettings.MinAdaptiveBackgroundUpdateIntervalFrames,
            int.MaxValue);
        var plc = new PlcSettings(
            BaseUrl: SettingsPlcBaseUrlTextBox.Text ?? appSettings.Plc.BaseUrl,
            User: SettingsPlcUserTextBox.Text ?? appSettings.Plc.User,
            Password: SettingsPlcPasswordTextBox.Text ?? appSettings.Plc.Password);
        draftAppSettings = new AppSettings(
            columns,
            rows,
            adaptiveBackgroundSampleCount,
            adaptiveBackgroundUpdateIntervalFrames,
            plc);
    }

    private void RefreshSettingsWorkspaceUi()
    {
        SettingsGridColumnsPolicyText.Text = GetSettingsApplyPolicyLabel(SettingsField.GridColumns);
        SettingsGridRowsPolicyText.Text = GetSettingsApplyPolicyLabel(SettingsField.GridRows);
        SettingsAdaptiveBackgroundSampleCountPolicyText.Text = GetSettingsApplyPolicyLabel(SettingsField.AdaptiveBackgroundSampleCount);
        SettingsAdaptiveBackgroundUpdateIntervalFramesPolicyText.Text = GetSettingsApplyPolicyLabel(SettingsField.AdaptiveBackgroundUpdateIntervalFrames);
        SettingsPlcBaseUrlPolicyText.Text = GetSettingsApplyPolicyLabel(SettingsField.PlcBaseUrl);
        SettingsPlcUserPolicyText.Text = GetSettingsApplyPolicyLabel(SettingsField.PlcUser);
        SettingsPlcPasswordPolicyText.Text = GetSettingsApplyPolicyLabel(SettingsField.PlcPassword);
        SettingsGridColumnsTextBox.Text = draftAppSettings.GridColumns.ToString();
        SettingsGridRowsTextBox.Text = draftAppSettings.GridRows.ToString();
        SettingsAdaptiveBackgroundSampleCountTextBox.Text = draftAppSettings.AdaptiveBackgroundSampleCount.ToString();
        SettingsAdaptiveBackgroundUpdateIntervalFramesTextBox.Text = draftAppSettings.AdaptiveBackgroundUpdateIntervalFrames.ToString();
        SettingsPlcBaseUrlTextBox.Text = draftAppSettings.Plc.BaseUrl;
        SettingsPlcUserTextBox.Text = draftAppSettings.Plc.User;
        SettingsPlcPasswordTextBox.Text = draftAppSettings.Plc.Password;
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

    private void RefreshTrainWorkspaceUi()
    {
        List<ConfiguredTrain> snapshot;
        lock (settingsSync)
        {
            snapshot = configuredTrains.ToList();
        }

        TrainsListBox.ItemsSource = snapshot.Select(train => train.Name).ToList();
        if (snapshot.Count == 0)
        {
            selectedTrainIndex = -1;
            TrainsListBox.SelectedIndex = -1;
            SetTrainEditorEnabled(false);
            TrainEditorStatusText.Text = "No configured Trains.";
            return;
        }

        selectedTrainIndex = Math.Clamp(selectedTrainIndex, 0, snapshot.Count - 1);
        TrainsListBox.SelectedIndex = selectedTrainIndex;
        LoadTrainEditor(snapshot[selectedTrainIndex]);
        SetTrainEditorEnabled(true);
    }

    private void TrainsListBoxOnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (TrainsListBox.SelectedIndex < 0)
        {
            return;
        }

        selectedTrainIndex = TrainsListBox.SelectedIndex;
        ConfiguredTrain train;
        lock (settingsSync)
        {
            if (selectedTrainIndex >= configuredTrains.Count)
            {
                return;
            }

            train = configuredTrains[selectedTrainIndex];
        }

        LoadTrainEditor(train);
    }

    private void LoadTrainEditor(ConfiguredTrain train)
    {
        var projection = BuildTrainEditorProjection(train);
        TrainNameTextBox.Text = projection.Name;
        TrainPlcIdTextBox.Text = projection.PlcId;
        SetTrainPickerColors(projection.MinColor, projection.MaxColor);
        TrainMaxWidthTextBox.Text = projection.MaxWidth;
        TrainMaxHeightTextBox.Text = projection.MaxHeight;
        TrainHueLowerTextBox.Text = projection.HueLower;
        TrainHueUpperTextBox.Text = projection.HueUpper;
        TrainSaturationLowerTextBox.Text = projection.SaturationLower;
        TrainSaturationUpperTextBox.Text = projection.SaturationUpper;
        TrainValueLowerTextBox.Text = projection.ValueLower;
        TrainValueUpperTextBox.Text = projection.ValueUpper;
        TrainEditorStatusText.Text = "Train loaded.";
    }

    private void SetTrainEditorEnabled(bool isEnabled)
    {
        TrainNameTextBox.IsEnabled = isEnabled;
        TrainPlcIdTextBox.IsEnabled = isEnabled;
        TrainMinColorTextBox.IsEnabled = isEnabled;
        TrainMaxColorTextBox.IsEnabled = isEnabled;
        TrainMinColorPicker.IsEnabled = isEnabled;
        TrainMaxColorPicker.IsEnabled = isEnabled;
        TrainMaxWidthTextBox.IsEnabled = isEnabled;
        TrainMaxHeightTextBox.IsEnabled = isEnabled;
        TrainHueLowerTextBox.IsEnabled = isEnabled;
        TrainHueUpperTextBox.IsEnabled = isEnabled;
        TrainSaturationLowerTextBox.IsEnabled = isEnabled;
        TrainSaturationUpperTextBox.IsEnabled = isEnabled;
        TrainValueLowerTextBox.IsEnabled = isEnabled;
        TrainValueUpperTextBox.IsEnabled = isEnabled;
        SaveTrainButton.IsEnabled = isEnabled;
        DeleteTrainButton.IsEnabled = isEnabled;
    }

    private void ApplyTrainPresetToEditor(string preset)
    {
        var projection = ApplyTrainCalibrationPreset(preset);
        SetTrainPickerColors(projection.MinColor, projection.MaxColor);
        TrainHueLowerTextBox.Text = projection.HueLower;
        TrainHueUpperTextBox.Text = projection.HueUpper;
        TrainSaturationLowerTextBox.Text = projection.SaturationLower;
        TrainSaturationUpperTextBox.Text = projection.SaturationUpper;
        TrainValueLowerTextBox.Text = projection.ValueLower;
        TrainValueUpperTextBox.Text = projection.ValueUpper;
        TrainEditorStatusText.Text = $"{preset} preset applied.";
    }

    private void SetTrainPickerColors(uint minColor, uint maxColor)
    {
        applyingTrainColorPickerUi = true;
        TrainMinColorPicker.Color = ToAvaloniaColor(minColor);
        TrainMaxColorPicker.Color = ToAvaloniaColor(maxColor);
        applyingTrainColorPickerUi = false;
        RefreshTrainColorReadouts();
    }

    private void TrainColorPickerOnColorChanged(object? sender, EventArgs e)
    {
        if (applyingTrainColorPickerUi)
        {
            return;
        }

        RefreshTrainColorReadouts();
    }

    private void RefreshTrainColorReadouts()
    {
        TrainMinColorTextBox.Text = ToArgb(TrainMinColorPicker.Color).ToString("X8", CultureInfo.InvariantCulture);
        TrainMaxColorTextBox.Text = ToArgb(TrainMaxColorPicker.Color).ToString("X8", CultureInfo.InvariantCulture);
    }

    private void AddTrainButtonOnClick(object? sender, RoutedEventArgs e)
    {
        var train = new ConfiguredTrain(
            Guid.NewGuid(),
            "New Train",
            string.Empty,
            0xFFC8C8C8,
            0xFFFFFFFF,
            160,
            80,
            new ColorCalibrationProfile("New Train", 0, 180, 0, 50, 190, 255));

        lock (settingsSync)
        {
            configuredTrains.Add(train);
            selectedTrainIndex = configuredTrains.Count - 1;
        }

        PersistTrains();
        MarkTrainConfigurationChanged("Status: train added.");
        RefreshTrainWorkspaceUi();
    }

    private void DeleteTrainButtonOnClick(object? sender, RoutedEventArgs e)
    {
        lock (settingsSync)
        {
            if (selectedTrainIndex < 0 || selectedTrainIndex >= configuredTrains.Count)
            {
                return;
            }

            configuredTrains.RemoveAt(selectedTrainIndex);
            selectedTrainIndex = Math.Min(selectedTrainIndex, configuredTrains.Count - 1);
        }

        PersistTrains();
        MarkTrainConfigurationChanged("Status: train deleted.");
        RefreshTrainWorkspaceUi();
    }

    private void SaveTrainButtonOnClick(object? sender, RoutedEventArgs e)
    {
        var name = (TrainNameTextBox.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            TrainEditorStatusText.Text = "Train name is required.";
            return;
        }

        var train = new ConfiguredTrain(
            GetSelectedTrainId(),
            name,
            (TrainPlcIdTextBox.Text ?? string.Empty).Trim(),
            ToArgb(TrainMinColorPicker.Color),
            ToArgb(TrainMaxColorPicker.Color),
            ParseInt(TrainMaxWidthTextBox.Text, 160, 1, 100000),
            ParseInt(TrainMaxHeightTextBox.Text, 80, 1, 100000),
            NormalizeColorCalibration(new ColorCalibrationProfile(
                name,
                ParseInt(TrainHueLowerTextBox.Text, 0, 0, 180),
                ParseInt(TrainHueUpperTextBox.Text, 180, 0, 180),
                ParseInt(TrainSaturationLowerTextBox.Text, 0, 0, 255),
                ParseInt(TrainSaturationUpperTextBox.Text, 255, 0, 255),
                ParseInt(TrainValueLowerTextBox.Text, 0, 0, 255),
                ParseInt(TrainValueUpperTextBox.Text, 255, 0, 255))));

        lock (settingsSync)
        {
            if (selectedTrainIndex < 0 || selectedTrainIndex >= configuredTrains.Count)
            {
                configuredTrains.Add(train);
                selectedTrainIndex = configuredTrains.Count - 1;
            }
            else
            {
                configuredTrains[selectedTrainIndex] = train;
            }
        }

        PersistTrains();
        MarkTrainConfigurationChanged("Status: train saved.");
        RefreshTrainWorkspaceUi();
    }

    private Guid GetSelectedTrainId()
    {
        lock (settingsSync)
        {
            if (selectedTrainIndex >= 0 && selectedTrainIndex < configuredTrains.Count)
            {
                return configuredTrains[selectedTrainIndex].Id;
            }
        }

        return Guid.NewGuid();
    }

    private void PersistTrains()
    {
        List<ConfiguredTrain> snapshot;
        lock (settingsSync)
        {
            snapshot = configuredTrains.ToList();
        }

        trainStore.Save(snapshot);
    }

    private void MarkTrainConfigurationChanged(string status)
    {
        if (runTask is not null)
        {
            hasPendingVisionPipelineRestart = true;
            UpdateBottomStatusBar();
        }

        SetStatus(status);
    }

    private async void PlcConnectButtonOnClick(object? sender, RoutedEventArgs e)
    {
        if (plcClient is null || plcSessionManager is null)
        {
            AppendLog("PLC client not initialized.");
            PlcOperationLogText.Text = "PLC client not initialized.";
            return;
        }

        PlcConnectButton.IsEnabled = false;
        PlcReadButton.IsEnabled = false;
        PlcWriteButton.IsEnabled = false;
        PlcConnectionStatusText.Text = "Connecting...";

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await plcSessionManager.EnsureAuthenticatedAsync(cts.Token);
            var isAuthenticated = await plcSessionManager.IsAuthenticatedAsync(cts.Token);

            if (isAuthenticated)
            {
                _isPlcConnected = true;
                PlcConnectionStatusText.Text = "Connected";
                PlcReadButton.IsEnabled = true;
                PlcWriteButton.IsEnabled = true;
                AppendLog("PLC: connected.");
            }
            else
            {
                _isPlcConnected = false;
                PlcConnectionStatusText.Text = "Connection failed";
                AppendLog("PLC: connection failed.");
            }
        }
        catch (PlcException ex)
        {
            _isPlcConnected = false;
            PlcConnectionStatusText.Text = $"PLC error: {ex.Message}";
            AppendLog($"PLC connect error ({ex.Code}): {ex.Message}");
        }
        catch (OperationCanceledException)
        {
            _isPlcConnected = false;
            PlcConnectionStatusText.Text = "Connection timed out";
            AppendLog("PLC connect: request timed out (10s).");
        }
        catch (Exception ex)
        {
            _isPlcConnected = false;
            PlcConnectionStatusText.Text = $"Error: {ex.Message}";
            AppendLog($"PLC connect error: {ex.Message}");
        }
        finally
        {
            PlcConnectButton.IsEnabled = true;
        }
    }

    private async void PlcReadButtonOnClick(object? sender, RoutedEventArgs e)
    {
        if (plcClient is null)
        {
            AppendLog("PLC read: client not initialized.");
            PlcOperationLogText.Text = "PLC read: client not initialized.";
            return;
        }

        AppendLog($"PLC read button clicked; client={plcClient.GetType().Name}.");
        PlcOperationLogText.Text = $"PLC read button clicked; client={plcClient.GetType().Name}.";

        var variableName = PlcReadVariableTextBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(variableName))
        {
            AppendLog("PLC read: variable name is empty.");
            PlcOperationLogText.Text = "PLC read: variable name is empty.";
            return;
        }

        var typeIndex = PlcReadTypeComboBox.SelectedIndex;
        var plcType = typeIndex switch
        {
            0 => Plc.Model.PlcVariableType.Int16,
            1 => Plc.Model.PlcVariableType.Int32,
            2 => Plc.Model.PlcVariableType.Real,
            3 => Plc.Model.PlcVariableType.Bool,
            _ => Plc.Model.PlcVariableType.Int32
        };

        var variable = new Plc.Model.PlcVariable(variableName, plcType);

        try
        {
            AppendLog($"PLC read: {variable.Address} [{plcType}]...");
            PlcOperationLogText.Text = $"Reading: {variable.Address}...";
            PlcReadButton.IsEnabled = false;
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var result = await plcClient.ReadAsync(variable, cts.Token);
            AppendLog($"PLC read: {variable.Address} = {result.Value.Raw} ({result.Value.Type})");
            PlcOperationLogText.Text = $"Last read: {variable.Address} = {result.Value.Raw}";
        }
        catch (PlcException ex)
        {
            AppendLog($"PLC read error ({ex.Code}): {ex.Message}");
            PlcOperationLogText.Text = $"Error: {ex.Message}";
        }
        catch (OperationCanceledException)
        {
            AppendLog("PLC read: request timed out (10s).");
            PlcOperationLogText.Text = "Error: timed out";
        }
        catch (Exception ex)
        {
            AppendLog($"PLC read error: {ex.Message}");
            PlcOperationLogText.Text = $"Error: {ex.Message}";
        }
        finally
        {
            PlcReadButton.IsEnabled = true;
        }
    }

    private async void PlcWriteButtonOnClick(object? sender, RoutedEventArgs e)
    {
        if (plcClient is null)
        {
            AppendLog("PLC write: client not initialized.");
            PlcOperationLogText.Text = "PLC write: client not initialized.";
            return;
        }

        AppendLog($"PLC write button clicked; client={plcClient.GetType().Name}.");
        PlcOperationLogText.Text = $"PLC write button clicked; client={plcClient.GetType().Name}.";

        var variableName = PlcWriteVariableTextBox.Text?.Trim();
        var valueText = PlcWriteValueTextBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(variableName) || string.IsNullOrWhiteSpace(valueText))
        {
            AppendLog("PLC write: variable name or value is empty.");
            PlcOperationLogText.Text = "PLC write: variable name or value is empty.";
            return;
        }

        var typeIndex = PlcWriteTypeComboBox.SelectedIndex;
        var plcType = typeIndex switch
        {
            0 => Plc.Model.PlcVariableType.Int16,
            1 => Plc.Model.PlcVariableType.Int32,
            2 => Plc.Model.PlcVariableType.Real,
            3 => Plc.Model.PlcVariableType.Bool,
            _ => Plc.Model.PlcVariableType.Int32
        };

        try
        {
            var variable = new Plc.Model.PlcVariable(variableName, plcType);
            PlcValue value = plcType switch
            {
                Plc.Model.PlcVariableType.Int16 => PlcValue.Int16(short.Parse(valueText, CultureInfo.InvariantCulture)),
                Plc.Model.PlcVariableType.Int32 => PlcValue.Int32(int.Parse(valueText, CultureInfo.InvariantCulture)),
                Plc.Model.PlcVariableType.Real => PlcValue.Real(float.Parse(valueText, CultureInfo.InvariantCulture)),
                Plc.Model.PlcVariableType.Bool => PlcValue.Bool(bool.Parse(valueText)),
                _ => throw new InvalidOperationException($"Unsupported type: {plcType}")
            };

            AppendLog($"PLC write: {variable.Address} = {value.Raw} ({plcType})...");
            PlcOperationLogText.Text = $"Writing: {variable.Address} = {value.Raw}...";
            PlcWriteButton.IsEnabled = false;
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await plcClient.WriteAsync(new[] { (variable, value) }, cts.Token);
            AppendLog($"PLC write: {variable.Address} = {value.Raw} (success)");
            PlcOperationLogText.Text = $"Last write: {variable.Address} = {value.Raw}";
        }
        catch (PlcException ex)
        {
            AppendLog($"PLC write error ({ex.Code}): {ex.Message}");
            PlcOperationLogText.Text = $"Error: {ex.Message}";
        }
        catch (OperationCanceledException)
        {
            AppendLog("PLC write: request timed out (10s).");
            PlcOperationLogText.Text = "Error: timed out";
        }
        catch (Exception ex)
        {
            AppendLog($"PLC write error: {ex.Message}");
            PlcOperationLogText.Text = $"Error: {ex.Message}";
        }
        finally
        {
            PlcWriteButton.IsEnabled = true;
        }
    }

    private void PlcClearLogButtonOnClick(object? sender, RoutedEventArgs e)
    {
        plcLogEntries.Clear();
        PlcOperationLogText.Text = "No operations yet.";
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

        if (activeWorkspace == Workspace.Regions)
        {
            var camera = GetSelectedCamera();
            if (camera is not null)
            {
                RefreshRegionsList(camera.Value);
            }
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
        var dialog = new UsbCameraChoiceDialog();
        var selectedDevice = await dialog.ShowDialog<UsbCameraDevice?>(this);
        if (selectedDevice is null)
        {
            return;
        }

        var device = selectedDevice.Value;
        var cameraId = device.Id;
        var displayName = device.DisplayName;

        lock (cameraSync)
        {
            if (cameras.Any(c => string.Equals(c.Id, cameraId, StringComparison.OrdinalIgnoreCase)))
            {
                SetStatus($"Status: USB camera '{displayName}' is already added.");
                return;
            }

            cameras.Add(CameraProfile.CreateUsbCamera(device));
            cameraZoneIdentityService.AssignSourceToZone(cameraId, requestedZoneName: displayName);

            if (!cameraSettings.ContainsKey(cameraId))
            {
                cameraSettings[cameraId] = RuntimeProcessingSettings.Default;
            }

            if (selectedCameraIndex < 0 && cameras.Count > 0)
            {
                selectedCameraIndex = 0;
            }
        }

        PersistCameraSettings();
        PersistCameraZones();
        RefreshCameraUi();
        SetStatus($"Status: added USB camera '{displayName}'.");

        if (runTask is not null)
        {
            StartBakeForAllCameras(runCts?.Token ?? CancellationToken.None);
        }
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
            CameraDebugViewCheckBox.IsChecked = NormalizeDebugViewEnabled(camera.Value.IsIncludedInVisionPipeline, isVisionPipelineRunningForUi, camera.Value.DebugViewEnabled);
            applyingCameraDebugViewUi = false;
            CameraDebugViewCheckBox.IsEnabled = camera.Value.IsIncludedInVisionPipeline && isVisionPipelineRunningForUi;
            applyingShowRegionsUi = true;
            ShowRegionsCheckBox.IsChecked = camera.Value.ShowRegionsEnabled;
            applyingShowRegionsUi = false;
        }

        if (activeWorkspace == Workspace.Regions && camera is not null)
        {
            RefreshRegionsList(camera.Value);
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
        EnableDebugViewForIncludedCameras(DebugViewFrameType.ColorDetections);
        await StopCameraTilePreviewAsync();
        RefreshCameraUi();
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
            DisableDebugViewForAllCameras();
            SetRunState(isRunning: false);
            RefreshCameraUi();
        }
    }

    private void DisableDebugViewForAllCameras()
    {
        lock (cameraSync)
        {
            for (var i = 0; i < cameras.Count; i++)
            {
                cameras[i] = cameras[i] with { DebugViewEnabled = false };
            }
        }
    }

    private void EnableDebugViewForIncludedCameras(DebugViewFrameType frameType)
    {
        lock (cameraSync)
        {
            for (var i = 0; i < cameras.Count; i++)
            {
                if (!cameras[i].IsIncludedInVisionPipeline)
                {
                    continue;
                }

                cameras[i] = cameras[i] with
                {
                    DebugViewEnabled = true,
                    DebugViewFrameType = frameType
                };
            }
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

        UpdateSelectedCameraSettingsFromUi(logChange: false);
        var settings = GetSettingsForCamera(camera.Value.Id);
        var options = new BackgroundEstimationEngine.ProcessingOptions(
            settings.ProcessMaxWidth,
            settings.MotionArea,
            settings.ColorMinPixels,
            settings.MorphKernelSize,
            appSettings.AdaptiveBackgroundSampleCount,
            appSettings.AdaptiveBackgroundUpdateIntervalFrames,
            GetConfiguredTrainProfiles());

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
        if (!cameraZoneIdentityService.TryGetCameraZoneForSource(camera.Id, out var zone))
        {
            await Dispatcher.UIThread.InvokeAsync(() => SetStatus($"Status: no zone for camera {camera.DisplayName}"));
            return;
        }

        var zoneId = new ObjectTracker.UI.Desktop.Region.Model.CameraZoneId(zone.CameraZoneId);
        var regionProcessor = CreateRegionProcessorForZone(zoneId);

        engine.OnTrainDetected += detection =>
        {
            var trainDetection = new ObjectTracker.UI.Desktop.Region.Contracts.TrainDetection(
                GlobalTrainId: detection.Train.Id,
                LocalTrainId: new System.Guid(detection.LocalTrainId.ToString().PadLeft(32, '0')),
                TrainColor: detection.Train.Name,
                PixelX: detection.PositionX,
                PixelY: detection.PositionY,
                ProcessWidth: detection.BoundingBoxWidth,
                ProcessHeight: detection.BoundingBoxHeight,
                GridCols: appSettings.GridColumns,
                GridRows: appSettings.GridRows,
                Confidence: detection.Confidence,
                MotionState: "moving",
                ImageWidth: detection.FrameWidth,
                ImageHeight: detection.FrameHeight);

            var enriched = regionProcessor.ProcessDetection(trainDetection, zoneId.Value);
            if (!enriched.HasValue || enriched.Value.TransitionEvents.Count == 0)
            {
                return;
            }

            var regions = regionRegistry.GetByZone(zoneId);
            foreach (var transitionEvent in enriched.Value.TransitionEvents)
            {
                var transitionRegionName = ExtractTransitionRegionName(transitionEvent) ?? enriched.Value.ActiveRegionName;
                if (string.IsNullOrWhiteSpace(transitionRegionName))
                {
                    AppendPlcLog($"Transition region missing: {transitionEvent}");
                    continue;
                }

                var matchingRegion = regions.FirstOrDefault(r => r.Name == transitionRegionName);
                if (matchingRegion.Id == Guid.Empty)
                {
                    AppendPlcLog($"Region not found: {transitionRegionName}");
                    continue;
                }

                var type = matchingRegion.Type;
                if (type != RegionType.EnterCrossroadRegion &&
                    type != RegionType.ExitCrossroadRegion)
                {
                    AppendPlcLog($"Not an enter/exit region: {matchingRegion.Name} (type={type})");
                    continue;
                }

                WritePlcTransition(matchingRegion.Name, detection.Train.PlcId);
            }
        };

        if (camera.SourceKind == CameraSourceKind.UsbCamera)
        {
            await ProcessUsbCameraAsync(camera, engine, zoneId, cancellationToken);
            return;
        }

        for (var videoIndex = 0; !cancellationToken.IsCancellationRequested; videoIndex++)
        {
            var settings = GetSettingsForCamera(camera.Id);
            var options = BuildProcessingOptions(settings, zoneId);

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

            await using var source = new VideoFileSource(videoPath, camera.DisplayName);
            var result = await engine.ProcessAsync(
                source,
                settings.SampleCount,
                settings.Threshold,
                options,
                GetBakeImagePath(settings),
                onFrame: frameSet =>
                {
                    CacheLatestFrame(camera.Id, frameSet.MovingColorJpeg);
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

    private async Task ProcessUsbCameraAsync(
        CameraProfile camera,
        BackgroundEstimationEngine engine,
        ObjectTracker.UI.Desktop.Region.Model.CameraZoneId zoneId,
        CancellationToken cancellationToken)
    {
        for (; !cancellationToken.IsCancellationRequested;)
        {
            var settings = GetSettingsForCamera(camera.Id);
            var options = BuildProcessingOptions(settings, zoneId);

            await Dispatcher.UIThread.InvokeAsync(() => CurrentVideoText.Text = BuildCurrentSourceText(camera, camera.DisplayName));

            await using var source = CameraSourceFactory.Create(camera);
            var result = await engine.ProcessAsync(
                source,
                settings.SampleCount,
                settings.Threshold,
                options,
                GetBakeImagePath(settings),
                onFrame: frameSet =>
                {
                    CacheLatestFrame(camera.Id, frameSet.MovingColorJpeg);
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
            var settings = GetSettingsForCamera(camera.Id);
            var options = new BackgroundEstimationEngine.ProcessingOptions(
                settings.ProcessMaxWidth,
                settings.MotionArea,
                settings.ColorMinPixels,
                settings.MorphKernelSize,
                appSettings.AdaptiveBackgroundSampleCount,
                appSettings.AdaptiveBackgroundUpdateIntervalFrames,
                GetConfiguredTrainProfiles());

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

        if (!cameraTileDebugFrameTypesById.TryGetValue(cameraId, out var frameType))
        {
            return;
        }

        var targetImage = frameType switch
        {
            DebugViewFrameType.MovingColor => frameSet.MovingColorJpeg,
            DebugViewFrameType.ColorDetections => frameSet.ColorDetectionJpeg,
            _ => frameSet.MovingColorJpeg
        };

        targetImage = ApplyTileOverlaysIfEnabled(cameraId, targetImage);

        UpdatePreviewImage(debugImages.DebugPreview, targetImage);
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
            ShowRegionsCheckBox.IsChecked = false;
            ShowRegionsCheckBox.IsEnabled = false;
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
        CameraDebugViewCheckBox.IsEnabled = selected.IsIncludedInVisionPipeline && isVisionPipelineRunningForUi;
        applyingCameraDebugViewUi = true;
        CameraDebugViewCheckBox.IsChecked = NormalizeDebugViewEnabled(selected.IsIncludedInVisionPipeline, isVisionPipelineRunningForUi, selected.DebugViewEnabled);
        DebugViewFrameTypeComboBox.IsVisible = CameraDebugViewCheckBox.IsChecked == true;
        DebugViewFrameTypeComboBox.SelectedIndex = selected.DebugViewFrameType == DebugViewFrameType.ColorDetections ? 1 : 0;
        applyingCameraDebugViewUi = false;
        applyingShowRegionsUi = true;
        ShowRegionsCheckBox.IsChecked = selected.ShowRegionsEnabled;
        applyingShowRegionsUi = false;
        ShowRegionsCheckBox.IsEnabled = true;
    }

    private void RefreshCameraWorkspaceTiles(IReadOnlyList<CameraProfile> orderedCameras)
    {
        var projection = BuildCameraGridProjection(orderedCameras
            .Select(camera => new CameraWorkspaceCamera(
                camera.Id,
                camera.DisplayName,
                camera.IsVisible,
                camera.IsIncludedInVisionPipeline,
                NormalizeDebugViewEnabled(camera.IsIncludedInVisionPipeline, isVisionPipelineRunningForUi, camera.DebugViewEnabled),
                camera.DebugViewFrameType,
                camera.ShowRegionsEnabled))
            .ToList());

        var regionsList = BuildShowRegionsList(orderedCameras
            .Select(camera => new CameraWorkspaceCamera(
                camera.Id,
                camera.DisplayName,
                camera.IsVisible,
                camera.IsIncludedInVisionPipeline,
                NormalizeDebugViewEnabled(camera.IsIncludedInVisionPipeline, isVisionPipelineRunningForUi, camera.DebugViewEnabled),
                camera.DebugViewFrameType,
                camera.ShowRegionsEnabled))
            .ToList());

        var viewState = BuildCameraTileViewState(projection, regionsList);
        ApplyCameraTileViewState(viewState);
        StartCameraTilePreview(orderedCameras, projection);
    }

    private void StartCameraTilePreview(IReadOnlyList<CameraProfile> orderedCameras, CameraGridProjection projection)
    {
        var requests = BuildCameraTileFeedRequests(
            orderedCameras,
            projection,
            cameraTileImagesById.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase),
            runTask is not null);
        var requestIds = requests.Select(request => request.CameraId).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var feedCameras = orderedCameras
            .Where(camera => requestIds.Contains(camera.Id))
            .ToList();

        cameraTileFeedCamerasById.Clear();
        foreach (var camera in feedCameras)
        {
            cameraTileFeedCamerasById[camera.Id] = camera;
        }

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
            await using var videoSource = CameraSourceFactory.Create(camera);

            while (!cancellationToken.IsCancellationRequested)
            {
                var snapshot = videoSource.ReadLatestFrame();
                if (snapshot is not null)
                {
                    latestCameraFramesById[camera.Id] = snapshot.Value;
                    RenderSnapshotToTile(camera.Id, target, snapshot.Value);
                }

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

    private void RenderSnapshotToTile(string cameraId, Image target, VideoFrameSnapshot snapshot)
    {
        var jpegBytes = ApplyTileOverlaysIfEnabled(cameraId, snapshot.EncodedJpeg);

        using var stream = new MemoryStream(jpegBytes);
        var bitmap = new Bitmap(stream);
        Dispatcher.UIThread.Post(() => UpdatePreviewBitmap(target, bitmap), DispatcherPriority.Background);
    }

    private byte[] ApplyTileOverlaysIfEnabled(string cameraId, byte[] rawJpeg)
    {
        var showRegions = cameraTileRegionsEnabledById.TryGetValue(cameraId, out var reg) && reg;
        return showRegions ? ApplyTileOverlays(cameraId, rawJpeg) : rawJpeg;
    }

    private byte[] ApplyTileOverlays(
        string cameraId,
        byte[] rawJpeg)
    {
        var result = rawJpeg;

        var regions = GetRegionsForCamera(cameraId);
        if (regions.Count > 0)
        {
            result = TileOverlayRenderer.DrawRegions(
                rawJpeg,
                appSettings.GridColumns,
                appSettings.GridRows,
                regions);
        }

        return result;
    }

    private IReadOnlyList<RegionOverlayInfo> GetRegionsForCamera(string cameraId)
    {
        try
        {
            if (!cameraZoneIdentityService.TryGetCameraZoneForSource(cameraId, out var zone))
                return Array.Empty<RegionOverlayInfo>();

            var zoneId = new ObjectTracker.UI.Desktop.Region.Model.CameraZoneId(zone.CameraZoneId);
            var regions = regionRegistry?.GetByZone(zoneId) ?? Array.Empty<ObjectTracker.UI.Desktop.Region.Model.RegionDefinition>();

            return regions
                .Where(r => r.Type == ObjectTracker.UI.Desktop.Region.Model.RegionType.EnterCrossroadRegion ||
                            r.Type == ObjectTracker.UI.Desktop.Region.Model.RegionType.ExitCrossroadRegion)
                .Select(r => new RegionOverlayInfo(
                    r.Name,
                    (int)r.Type,
                    r.Cells.ToList()))
                .ToList();
        }
        catch
        {
            return Array.Empty<RegionOverlayInfo>();
        }
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
        cameraTileFeedKindsById.Clear();
        cameraTileDebugImagesById.Clear();
        cameraTileRegionsEnabledById.Clear();

        for (var i = 0; i < viewState.CameraIds.Count; i++)
        {
            var cameraId = viewState.CameraIds[i];
            var image = existingImagesById.TryGetValue(cameraId, out var existingImage)
                ? existingImage
                : new Image { Stretch = Avalonia.Media.Stretch.Uniform };
            cameraTileImagesById[cameraId] = image;
            cameraTileFeedKindsById[cameraId] = viewState.FeedKinds[i];
            cameraTileRegionsEnabledById[cameraId] = viewState.ShowRegionsEnabled[i];

            var panel = new Panel();
            if (image.Parent is Panel currentParent)
            {
                currentParent.Children.Remove(image);
            }

            panel.Children.Add(image);

            if (viewState.FeedKinds[i] == FeedKind.DebugView)
            {
                var debugPreview = new Image { Stretch = Avalonia.Media.Stretch.UniformToFill };

                cameraTileDebugImagesById[viewState.CameraIds[i]] = new DebugTileImageSet(debugPreview);
                cameraTileDebugFrameTypesById[viewState.CameraIds[i]] = viewState.DebugViewFrameTypes[i];

                panel.Children.Add(debugPreview);
            }

            var modeBadge = new TextBlock
            {
                Text = GetFeedKindBadge(viewState.FeedKinds[i]),
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

            if (viewState.FeedKinds[i] != FeedKind.RawFeed)
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
                DebugViewEnabled = NormalizeDebugViewEnabled(isIncluded, isVisionPipelineRunningForUi, selected.DebugViewEnabled)
            };
        }

        applyingCameraDebugViewUi = true;
        CameraDebugViewCheckBox.IsEnabled = VisionPipelineInclusionCheckBox.IsChecked == true && isVisionPipelineRunningForUi;
        CameraDebugViewCheckBox.IsChecked = VisionPipelineInclusionCheckBox.IsChecked == true && isVisionPipelineRunningForUi && CameraDebugViewCheckBox.IsChecked == true;
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
                DebugViewEnabled = NormalizeDebugViewEnabled(selected.IsIncludedInVisionPipeline, isVisionPipelineRunningForUi, debugEnabled)
            };
        }

        applyingCameraDebugViewUi = true;
        DebugViewFrameTypeComboBox.IsVisible = CameraDebugViewCheckBox.IsChecked == true;
        applyingCameraDebugViewUi = false;

        RefreshCameraUi();
    }

    private void DebugViewFrameTypeComboBoxOnSelectionChanged(object? sender, SelectionChangedEventArgs e)
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

            var selectedIndex = DebugViewFrameTypeComboBox.SelectedIndex;
            if (selectedIndex < 0)
            {
                return;
            }

            var frameType = selectedIndex == 0 ? DebugViewFrameType.MovingColor : DebugViewFrameType.ColorDetections;
            var selected = cameras[selectedCameraIndex];
            cameras[selectedCameraIndex] = selected with
            {
                DebugViewFrameType = frameType
            };
        }

        RefreshCameraUi();
    }

    private void ShowRegionsCheckBoxOnChanged(object? sender, RoutedEventArgs e)
    {
        if (applyingShowRegionsUi)
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
            var showRegions = ShowRegionsCheckBox.IsChecked == true;
            cameras[selectedCameraIndex] = selected with { ShowRegionsEnabled = showRegions };
        }

        RefreshCameraUi();
    }

    private void ApplyCameraTileGridDimensions(int rows, int columns)
    {
        CameraTileGrid.Rows = Math.Max(1, rows);
        CameraTileGrid.Columns = Math.Max(1, columns);
    }

    private void SetRunState(bool isRunning)
    {
        isVisionPipelineRunningForUi = isRunning;
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
        var dialog = new ConfirmationDialog(title, message);
        var result = await dialog.ShowDialog<ConfirmationDialog.ConfirmationResult>(this);
        return result.IsConfirmed;
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
                bakeImagePath);
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
            settings.MorphKernelSize);
    }

    private BackgroundEstimationEngine.ProcessingOptions BuildProcessingOptions(
        RuntimeProcessingSettings settings,
        ObjectTracker.UI.Desktop.Region.Model.CameraZoneId? zoneId = null)
    {
        var excludeCells = Array.Empty<ObjectTracker.UI.Desktop.Region.Model.GridCell>();
        if (zoneId.HasValue)
        {
            excludeCells = regionRegistry.GetByZone(zoneId.Value)
                .Where(region => region.Type == RegionType.ExcludeRegion)
                .SelectMany(region => region.Cells)
                .Distinct()
                .ToArray();
        }

        return new BackgroundEstimationEngine.ProcessingOptions(
            settings.ProcessMaxWidth,
            settings.MotionArea,
            settings.ColorMinPixels,
            settings.MorphKernelSize,
            appSettings.AdaptiveBackgroundSampleCount,
            appSettings.AdaptiveBackgroundUpdateIntervalFrames,
            GetConfiguredTrainProfiles(),
            appSettings.GridColumns,
            appSettings.GridRows,
            excludeCells);
    }

    private IReadOnlyList<TrainDetectionProfile> GetConfiguredTrainProfiles()
    {
        lock (settingsSync)
        {
            return TrainDetectionProfile.FromConfiguredTrains(configuredTrains.ToList());
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
        OpenBakedMaskButton.IsEnabled = settings.BakeSourceMode == BakeSourceMode.Samples || !string.IsNullOrWhiteSpace(settings.BakeImagePath);
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

    private static Color ToAvaloniaColor(uint argb)
    {
        return Color.FromArgb(
            (byte)((argb >> 24) & 0xFF),
            (byte)((argb >> 16) & 0xFF),
            (byte)((argb >> 8) & 0xFF),
            (byte)(argb & 0xFF));
    }

    private static uint ToArgb(Color color)
    {
        return ((uint)color.A << 24)
            | ((uint)color.R << 16)
            | ((uint)color.G << 8)
            | color.B;
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

    private static string BuildSafeFileName(string value, string fallback)
    {
        var name = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        foreach (var invalidChar in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalidChar, '-');
        }

        return string.IsNullOrWhiteSpace(name) ? fallback : name;
    }

    internal enum BakeSourceMode
    {
        Samples = 0,
        ImageFile = 1
    }

    internal enum CameraSourceKind
    {
        VideoFile = 0,
        UsbCamera = 1
    }

    internal readonly record struct CameraProfile(
        string Id,
        string DisplayName,
        bool IsVisible,
        bool IsIncludedInVisionPipeline,
        bool DebugViewEnabled,
        List<string> VideoPaths,
        CameraSourceKind SourceKind = CameraSourceKind.VideoFile,
        int? UsbDeviceIndex = null,
        DebugViewFrameType DebugViewFrameType = DebugViewFrameType.MovingColor,
        bool ShowRegionsEnabled = false)
    {
        public string PrimaryVideoPath => VideoPaths.Count > 0 ? VideoPaths[0] : string.Empty;

        public bool CanOpenBakedMask => !string.IsNullOrWhiteSpace(PrimaryVideoPath);

        public string CurrentSourceLabel => string.IsNullOrWhiteSpace(PrimaryVideoPath) ? DisplayName : Path.GetFileName(PrimaryVideoPath);

        public static CameraProfile CreateVideo(string id, string displayName, List<string> videoPaths)
            => new(id, displayName, true, true, false, videoPaths);

        public static CameraProfile CreateUsbCamera(UsbCameraDevice device)
            => new(device.Id, device.DisplayName, true, true, false, new List<string>(), CameraSourceKind.UsbCamera, device.DeviceIndex);
    }

    internal readonly record struct RuntimeProcessingSettings(
        int SampleCount,
        int Threshold,
        int MotionArea,
        int ColorMinPixels,
        int MorphKernelSize,
        int ProcessMaxWidth,
        BakeSourceMode BakeSourceMode,
        string BakeImagePath)
    {
        public static RuntimeProcessingSettings Default => new(20, 100, 220, 40, 3, 640, BakeSourceMode.Samples, string.Empty);
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

    private ObjectTracker.UI.Desktop.Region.Contracts.IRegionProcessorService CreateRegionProcessorForZone(
        ObjectTracker.UI.Desktop.Region.Model.CameraZoneId zoneId)
    {
        var evaluator = new ObjectTracker.UI.Desktop.Region.Implementation.RegionEvaluator(
            new ObjectTracker.UI.Desktop.Region.Implementation.CoordinateMapper());

        var handoffResolver = new ObjectTracker.UI.Desktop.Region.Implementation.HandoffResolver();

        return new ObjectTracker.UI.Desktop.Region.Implementation.RegionProcessorService(
            evaluator,
            handoffResolver,
            regionRegistry);
    }

    private void WritePlcTransition(string regionName, string trainColor)
    {
        if (plcClient is null || string.IsNullOrWhiteSpace(regionName))
            return;

        _ = Task.Run(async () =>
        {
            try
            {
                var writer = new PlcTransitionWriter(plcClient, AppendPlcLog);
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await writer.WriteTransitionAsync(regionName, trainColor, _isPlcConnected, cts.Token);
            }
            catch (Exception ex)
            {
                AppendPlcLog($"PLC write error ({regionName}): {ex.Message}");
            }
        });
    }

    private static string? ExtractTransitionRegionName(string transitionEvent)
    {
        const string marker = "regionName=";
        var start = transitionEvent.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0)
        {
            return null;
        }

        start += marker.Length;
        var end = transitionEvent.IndexOf(',', start);
        return (end < 0 ? transitionEvent[start..] : transitionEvent[start..end]).Trim();
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
            () => LoadCameraFrameAsync(camera.Value));
    }

    private async Task<byte[]?> LoadCameraFrameAsync(CameraProfile camera)
    {
        try
        {
            if (latestCameraFramesById.TryGetValue(camera.Id, out var latestFrame))
            {
                return latestFrame.EncodedJpeg;
            }

            if (camera.SourceKind == CameraSourceKind.UsbCamera)
            {
                var deviceIndex = camera.UsbDeviceIndex ?? 0;
                using var capture = new VideoCapture(deviceIndex);
                if (capture.IsOpened())
                {
                    using var mat = new Mat();
                    if (capture.Read(mat) && !mat.Empty())
                    {
                        Cv2.ImEncode(".jpg", mat, out var jpegBytes, new[] { (int)ImwriteFlags.JpegQuality, 80 });
                        return jpegBytes;
                    }
                }

                return null;
            }

            if (!string.IsNullOrWhiteSpace(camera.PrimaryVideoPath))
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

    private void CacheLatestFrame(string cameraId, byte[] encodedJpeg)
    {
        latestCameraFramesById[cameraId] = new VideoFrameSnapshot(
            cameraId,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            0,
            0,
            encodedJpeg,
            0);
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
        RegionsListBox.ItemsSource = BuildRegionListText(zoneId, "No regions in this zone.", regions);
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

        await regionManagerService.OpenGridEditorAsync(zoneId, this, null, regionName, regionType);

        sessionAuditLogger.AppendEvent(
            SessionAuditLogger.EventRegionCreated,
            $"Region '{regionName}' created.",
            ("Zone", zone.CameraZoneId),
            ("Type", regionType.ToString()));

        SetStatus($"Status: region '{regionName}' created.");
        RefreshRegionsList(camera.Value);
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
        var regionName = selectedRegionText;
        var region = regions.FirstOrDefault(r => r.Name == regionName);

        if (region.Id == Guid.Empty)
            return;

        await regionManagerService.OpenGridEditorAsync(zoneId, this, region.Id, region.Name, region.Type);

        sessionAuditLogger.AppendEvent(
            SessionAuditLogger.EventRegionUpdated,
            $"Region '{region.Name}' edited.",
            ("Zone", zone.CameraZoneId));

        SetStatus($"Status: region '{region.Name}' edited.");
        RefreshRegionsList(camera.Value);
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
        var regionName = selectedRegionText;
        var region = regions.FirstOrDefault(r => r.Name == regionName);

        if (region.Id == Guid.Empty)
            return;

        var confirmed = await ConfirmDestructiveActionAsync("Delete Region", $"Are you sure you want to delete '{region.Name}'?");
        if (!confirmed)
            return;

        await regionManagerService.DeleteRegionAsync(region.Id);

        sessionAuditLogger.AppendEvent(
            SessionAuditLogger.EventRegionDeleted,
            $"Region '{region.Name}' deleted.",
            ("Zone", zone.CameraZoneId));

        SetStatus($"Status: region '{region.Name}' deleted.");
        RefreshRegionsList(camera.Value);

    }

    private void RefreshRegionsList(CameraProfile camera)
    {
        if (!cameraZoneIdentityService.TryGetCameraZoneForSource(camera.Id, out var zone))
            return;

        var zoneId = new ObjectTracker.UI.Desktop.Region.Model.CameraZoneId(zone.CameraZoneId);
        var regions = regionManagerService.GetRegionsForZone(zoneId);
        RegionsListBox.ItemsSource = BuildRegionListText(zoneId, "No regions in this zone.", regions);
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

    private async void ExportRegionButtonOnClick(object? sender, RoutedEventArgs e)
    {
        if (StorageProvider is null)
        {
            SetStatus("Status: file picker is not available in this runtime.");
            return;
        }

        var camera = GetSelectedCamera();
        if (camera is null || !cameraZoneIdentityService.TryGetCameraZoneForSource(camera.Value.Id, out var zone))
        {
            SetStatus("Status: select a camera first.");
            return;
        }

        var zoneId = new ObjectTracker.UI.Desktop.Region.Model.CameraZoneId(zone.CameraZoneId);
        var regions = regionManagerService.GetRegionsForZone(zoneId);
        if (regions.Count == 0)
        {
            SetStatus("Status: no regions to export in this zone.");
            return;
        }

        var safeName = string.IsNullOrWhiteSpace(zone.Name) ? "camerazone" : zone.Name.ToUpperInvariant().Replace(' ', '-');
        var saveOptions = new FilePickerSaveOptions
        {
            Title = "Export Region Definitions",
            DefaultExtension = "json",
            SuggestedFileName = $"{safeName}-regions.json",
            ShowOverwritePrompt = true
        };

        var file = await StorageProvider.SaveFilePickerAsync(saveOptions);
        if (file == null)
        {
            return;
        }

        var path = file.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            SetStatus("Status: could not resolve export file path.");
            return;
        }

        await regionManagerService.ExportZoneRegionsAsync(zoneId, path);

        sessionAuditLogger.AppendEvent(
            SessionAuditLogger.EventRegionExported,
            $"Regions exported to '{System.IO.Path.GetFileName(path)}'.",
            ("Zone", zone.CameraZoneId));

        SetStatus($"Status: {regions.Count} region(s) exported.");
    }

    private async void ImportRegionButtonOnClick(object? sender, RoutedEventArgs e)
    {
        if (StorageProvider is null)
        {
            SetStatus("Status: file picker is not available in this runtime.");
            return;
        }

        var camera = GetSelectedCamera();
        if (camera is null || !cameraZoneIdentityService.TryGetCameraZoneForSource(camera.Value.Id, out var zone))
        {
            SetStatus("Status: select a camera first.");
            return;
        }

        var zoneId = new ObjectTracker.UI.Desktop.Region.Model.CameraZoneId(zone.CameraZoneId);

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import Region Definitions",
            AllowMultiple = false,
            FileTypeFilter = new List<FilePickerFileType>
            {
                new ("JSON files") { Patterns = new[] { "*.json" } },
                new ("All files") { Patterns = new[] { "*.*", "*"} }
            }
        });

        if (files == null || files.Count == 0)
        {
            return;
        }

        var path = files[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            SetStatus("Status: could not resolve import file path.");
            return;
        }

        await regionManagerService.ImportZoneRegionsAsync(zoneId, path);

        sessionAuditLogger.AppendEvent(
            SessionAuditLogger.EventRegionImported,
            $"Regions imported from '{System.IO.Path.GetFileName(path)}'.",
            ("Zone", zone.CameraZoneId));

        SetStatus("Status: regions imported successfully.");
        RefreshRegionsList(camera.Value);
    }

    private async void ExportCameraRegionsButtonOnClick(object? sender, RoutedEventArgs e)
    {
        if (StorageProvider is null)
        {
            SetStatus("Status: file picker is not available in this runtime.");
            return;
        }

        var camera = GetSelectedCamera();
        if (camera is null || !cameraZoneIdentityService.TryGetCameraZoneForSource(camera.Value.Id, out var zone))
        {
            SetStatus("Status: select a camera first.");
            return;
        }

        var zoneId = new ObjectTracker.UI.Desktop.Region.Model.CameraZoneId(zone.CameraZoneId);
        var regions = regionManagerService.GetRegionsForZone(zoneId);
        if (regions.Count == 0)
        {
            SetStatus("Status: no regions to export for this camera.");
            return;
        }

        var saveOptions = new FilePickerSaveOptions
        {
            Title = "Export All Camera Regions",
            DefaultExtension = "json",
            SuggestedFileName = $"{BuildSafeFileName(camera.Value.DisplayName, "camera-source")}.json",
            ShowOverwritePrompt = true
        };

        var file = await StorageProvider.SaveFilePickerAsync(saveOptions);
        if (file == null)
        {
            return;
        }

        var path = file.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            SetStatus("Status: could not resolve export file path.");
            return;
        }

        await regionManagerService.ExportCameraRegionsAsync(zoneId, path);

        sessionAuditLogger.AppendEvent(
            SessionAuditLogger.EventRegionExported,
            $"Camera regions exported to '{System.IO.Path.GetFileName(path)}'.",
            ("Zone", zone.CameraZoneId),
            ("Camera", camera.Value.DisplayName));

        SetStatus($"Status: {regions.Count} camera region(s) exported.");
    }

    private async void ImportCameraRegionsButtonOnClick(object? sender, RoutedEventArgs e)
    {
        if (StorageProvider is null)
        {
            SetStatus("Status: file picker is not available in this runtime.");
            return;
        }

        var camera = GetSelectedCamera();
        if (camera is null || !cameraZoneIdentityService.TryGetCameraZoneForSource(camera.Value.Id, out var zone))
        {
            SetStatus("Status: select a camera first.");
            return;
        }

        var zoneId = new ObjectTracker.UI.Desktop.Region.Model.CameraZoneId(zone.CameraZoneId);
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import All Camera Regions",
            AllowMultiple = false,
            FileTypeFilter = new List<FilePickerFileType>
            {
                new ("JSON files") { Patterns = new[] { "*.json" } },
                new ("All files") { Patterns = new[] { "*.*", "*" } }
            }
        });

        if (files == null || files.Count == 0)
        {
            return;
        }

        var path = files[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            SetStatus("Status: could not resolve import file path.");
            return;
        }

        var importedCount = await regionManagerService.ImportCameraRegionsAsync(zoneId, path);

        sessionAuditLogger.AppendEvent(
            SessionAuditLogger.EventRegionImported,
            $"Camera regions imported from '{System.IO.Path.GetFileName(path)}'.",
            ("Zone", zone.CameraZoneId),
            ("Camera", camera.Value.DisplayName));

        SetStatus($"Status: {importedCount} camera region(s) imported.");
        RefreshRegionsList(camera.Value);
    }
}
