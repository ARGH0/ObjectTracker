using System;
using System.Collections.Generic;
using Avalonia.Controls;
using ObjectTracker.UI.Desktop;
using ObjectTracker.UI.Desktop.Region.Model;
using Xunit;

namespace ObjectTracker.UI.Desktop.Tests;

public sealed class MainWindowWorkspaceShellTests
{
    [Fact]
    public void BuildWorkspaceVisibility_Camera_ShowsOnlyCameraWorkspace()
    {
        var visibility = MainWindow.BuildWorkspaceVisibility(MainWindow.Workspace.Camera);

        Assert.True(visibility.CameraVisible);
        Assert.False(visibility.RegionsVisible);
        Assert.False(visibility.SettingsVisible);
    }

    [Fact]
    public void BuildWorkspaceVisibility_Layers_ShowsOnlyLayersWorkspace()
    {
        var visibility = MainWindow.BuildWorkspaceVisibility(MainWindow.Workspace.Regions);

        Assert.False(visibility.CameraVisible);
        Assert.True(visibility.RegionsVisible);
        Assert.False(visibility.TrainsVisible);
        Assert.False(visibility.SettingsVisible);
    }

    [Fact]
    public void BuildWorkspaceVisibility_Trains_ShowsOnlyTrainsWorkspace()
    {
        var visibility = MainWindow.BuildWorkspaceVisibility(MainWindow.Workspace.Trains);

        Assert.False(visibility.CameraVisible);
        Assert.False(visibility.RegionsVisible);
        Assert.True(visibility.TrainsVisible);
        Assert.False(visibility.SettingsVisible);
    }

    [Fact]
    public void BuildWorkspaceVisibility_Settings_ShowsOnlySettingsWorkspace()
    {
        var visibility = MainWindow.BuildWorkspaceVisibility(MainWindow.Workspace.Settings);

        Assert.False(visibility.CameraVisible);
        Assert.False(visibility.RegionsVisible);
        Assert.True(visibility.SettingsVisible);
    }

    [Fact]
    public void RuntimeLog_IsVisible_OnlyInCameraWorkspace()
    {
        Assert.True(MainWindow.IsRuntimeLogVisibleForWorkspace(MainWindow.Workspace.Camera));
        Assert.False(MainWindow.IsRuntimeLogVisibleForWorkspace(MainWindow.Workspace.Regions));
        Assert.False(MainWindow.IsRuntimeLogVisibleForWorkspace(MainWindow.Workspace.Settings));
    }

    [Fact]
    public void BottomStatusSnapshot_ShowsPendingRestartWhenRequired()
    {
        var snapshot = MainWindow.BuildBottomStatusSnapshot(
            isVisionPipelineRunning: true,
            hasPendingVisionPipelineRestart: true);

        Assert.Equal("Pending restart: required", snapshot.PendingRestart);
    }

    [Fact]
    public void VisionPipelineMenuState_WhenRunning_DisablesStartAndEnablesStop()
    {
        var state = MainWindow.BuildVisionPipelineMenuState(isVisionPipelineRunning: true);

        Assert.False(state.StartEnabled);
        Assert.True(state.StopEnabled);
    }

    [Fact]
    public void VisionPipelineMenuState_WhenStopped_EnablesStartAndDisablesStop()
    {
        var state = MainWindow.BuildVisionPipelineMenuState(isVisionPipelineRunning: false);

        Assert.True(state.StartEnabled);
        Assert.False(state.StopEnabled);
    }

    [Fact]
    public void CameraPanelLayoutState_WhenPinnedAndOpen_ClaimsLayoutSpace()
    {
        var state = MainWindow.BuildCameraPanelLayoutState(isOpen: true, isPinned: true);

        Assert.True(state.IsOpen);
        Assert.True(state.IsPinned);
        Assert.Equal(SplitViewDisplayMode.Inline, state.DisplayMode);
        Assert.Equal(340, state.CameraPanelWidth);
        Assert.Equal(0, state.CompactPaneWidth);
    }

    [Fact]
    public void CameraPanelLayoutState_WhenOverlayAndOpen_DoesNotClaimLayoutSpace()
    {
        var state = MainWindow.BuildCameraPanelLayoutState(isOpen: true, isPinned: false);

        Assert.True(state.IsOpen);
        Assert.False(state.IsPinned);
        Assert.Equal(SplitViewDisplayMode.CompactOverlay, state.DisplayMode);
        Assert.Equal(340, state.CameraPanelWidth);
        Assert.Equal(48, state.CompactPaneWidth);
    }

    [Fact]
    public void CameraPanelLayoutState_WhenPinned_IgnoresClosedStateAndStaysOpen()
    {
        var state = MainWindow.BuildCameraPanelLayoutState(isOpen: false, isPinned: true);

        Assert.True(state.IsOpen);
        Assert.Equal(SplitViewDisplayMode.Inline, state.DisplayMode);
        Assert.Equal(340, state.CameraPanelWidth);
        Assert.Equal(0, state.CompactPaneWidth);
    }

    [Fact]
    public void CameraPanelLayoutState_WhenClosedAndUnpinned_UsesOverlayWithoutCompactPane()
    {
        var state = MainWindow.BuildCameraPanelLayoutState(isOpen: false, isPinned: false);

        Assert.False(state.IsOpen);
        Assert.Equal(SplitViewDisplayMode.CompactOverlay, state.DisplayMode);
        Assert.Equal(340, state.CameraPanelWidth);
        Assert.Equal(48, state.CompactPaneWidth);
    }

    [Fact]
    public void CameraListSelectionMode_IsSingle()
    {
        Assert.Equal(SelectionMode.Single, MainWindow.GetCameraListSelectionMode());
    }

    [Fact]
    public void CameraDestructiveActionsState_WhenVisionPipelineRunning_DisablesClearAndDelete()
    {
        var state = MainWindow.BuildCameraDestructiveActionsState(
            isVisionPipelineRunning: true,
            hasSelectedCamera: true,
            cameraCount: 2);

        Assert.False(state.DeleteSelectedEnabled);
        Assert.False(state.ClearAllEnabled);
        Assert.True(state.DeleteSelectedRequiresConfirmation);
        Assert.True(state.ClearAllRequiresConfirmation);
    }

    [Fact]
    public void CameraDestructiveActionsState_WhenStoppedAndSelectionExists_EnablesDeleteAndClear()
    {
        var state = MainWindow.BuildCameraDestructiveActionsState(
            isVisionPipelineRunning: false,
            hasSelectedCamera: true,
            cameraCount: 1);

        Assert.True(state.DeleteSelectedEnabled);
        Assert.True(state.ClearAllEnabled);
    }

    [Fact]
    public void DeleteCameraConfirmationMessage_UsesSelectedCameraName()
    {
        var message = MainWindow.BuildDeleteCameraConfirmationMessage("Camera A");

        Assert.Equal("Delete camera 'Camera A' from this Session?", message);
    }

    [Fact]
    public void ClearCamerasConfirmationMessage_UsesCameraCountAndWarning()
    {
        var message = MainWindow.BuildClearCamerasConfirmationMessage(3);

        Assert.Equal("Clear all 3 cameras from this Session? This cannot be undone.", message);
    }

    [Fact]
    public void SettingsDraftState_WhenDraftDiffersFromSaved_HasUnsavedChangesWithoutChangingSavedSettings()
    {
        var saved = new AppSettings(GridColumns: 32, GridRows: 18);
        var draft = new AppSettings(GridColumns: 40, GridRows: 18);

        var state = MainWindow.BuildSettingsDraftState(saved, draft);

        Assert.True(state.HasUnsavedChanges);
        Assert.Equal(saved, state.SavedSettings);
        Assert.Equal(draft, state.DraftSettings);
        Assert.Equal("Settings: unsaved changes", state.StatusText);
    }

    [Fact]
    public void SettingsNavigation_WithUnsavedChangesAndCancel_StaysInSettingsWithDraftIntact()
    {
        var saved = new AppSettings(GridColumns: 32, GridRows: 18);
        var draft = new AppSettings(GridColumns: 40, GridRows: 18);

        var result = MainWindow.ApplySettingsNavigationDecision(
            MainWindow.Workspace.Settings,
            MainWindow.Workspace.Camera,
            saved,
            draft,
            MainWindow.SettingsNavigationDecision.Cancel);

        Assert.Equal(MainWindow.Workspace.Settings, result.Workspace);
        Assert.Equal(saved, result.SavedSettings);
        Assert.Equal(draft, result.DraftSettings);
        Assert.True(result.HasUnsavedChanges);
    }

    [Theory]
    [InlineData(MainWindow.SettingsNavigationDecision.Save, 40, true)]
    [InlineData(MainWindow.SettingsNavigationDecision.Discard, 32, false)]
    public void SettingsNavigation_WithUnsavedChanges_AppliesSaveOrDiscard(
        MainWindow.SettingsNavigationDecision decision,
        int expectedSavedColumns,
        bool expectedPersist)
    {
        var saved = new AppSettings(GridColumns: 32, GridRows: 18);
        var draft = new AppSettings(GridColumns: 40, GridRows: 18);

        var result = MainWindow.ApplySettingsNavigationDecision(
            MainWindow.Workspace.Settings,
            MainWindow.Workspace.Camera,
            saved,
            draft,
            decision);

        Assert.Equal(MainWindow.Workspace.Camera, result.Workspace);
        Assert.Equal(expectedSavedColumns, result.SavedSettings.GridColumns);
        Assert.Equal(result.SavedSettings, result.DraftSettings);
        Assert.False(result.HasUnsavedChanges);
        Assert.Equal(expectedPersist, result.ShouldPersist);
    }

    [Theory]
    [InlineData(MainWindow.SettingsField.GridColumns)]
    [InlineData(MainWindow.SettingsField.GridRows)]
    public void SettingsApplyPolicyLabel_ForGridSettings_RequiresVisionPipelineRestart(MainWindow.SettingsField field)
    {
        var label = MainWindow.GetSettingsApplyPolicyLabel(field);

        Assert.Equal("requires Vision Pipeline restart", label);
    }

    [Fact]
    public void SettingsSaveImpact_WhenRestartRequiredSettingChangesWhileRunning_MarksPendingRestart()
    {
        var saved = new AppSettings(GridColumns: 32, GridRows: 18);
        var draft = new AppSettings(GridColumns: 40, GridRows: 18);

        var impact = MainWindow.BuildSettingsSaveImpact(saved, draft, isVisionPipelineRunning: true);

        Assert.True(impact.RequiresVisionPipelineRestart);
        Assert.True(impact.HasPendingVisionPipelineRestart);
        Assert.Equal("Settings: saved, pending Vision Pipeline restart", impact.SettingsStatusText);
    }

    [Fact]
    public void BuildRegionListText_WithNoZone_ReturnsFallbackMessage()
    {
        var result = MainWindow.BuildRegionListText(null, "No camera zone selected.");

        Assert.Single(result);
        Assert.Equal("No camera zone selected.", result[0]);
    }

    [Fact]
    public void BuildRegionListText_WithEmptyRegions_ReturnsFallbackMessage()
    {
        var result = MainWindow.BuildRegionListText(
            new CameraZoneId("zone-1"), "No regions", Array.Empty<RegionDefinition>());

        Assert.Single(result);
        Assert.Equal("No regions", result[0]);
    }

    [Fact]
    public void BuildRegionListText_WithRegions_FormatsCorrectly()
    {
        var regions = new List<RegionDefinition>
        {
            new(
                Id: Guid.NewGuid(), Name: "North Crossing Entry", Type: RegionType.EnterCrossroadRegion,
                CameraZoneId: new CameraZoneId("zone-1"),
                Cells: new List<GridCell> { new(5, 3), new(6, 3) },
                CreatedAt: DateTime.UtcNow, UpdatedAt: DateTime.UtcNow, OverlappingZoneIds: null),
            new(
                Id: Guid.NewGuid(), Name: "Platform Exclusion", Type: RegionType.ExcludeRegion,
                CameraZoneId: new CameraZoneId("zone-1"),
                Cells: new List<GridCell> { new(10, 0) },
                CreatedAt: DateTime.UtcNow, UpdatedAt: DateTime.UtcNow, OverlappingZoneIds: null),
        };

        var result = MainWindow.BuildRegionListText(
            new CameraZoneId("zone-1"), "No regions", regions);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, r => r == "North Crossing Entry");
        Assert.Contains(result, r => r == "Platform Exclusion");
    }

    [Fact]
    public void BuildRegionListText_WithRegions_DoesNotMutateInput()
    {
        var regions = new List<RegionDefinition>
        {
            new(
                Id: Guid.NewGuid(), Name: "Test", Type: RegionType.ExcludeRegion,
                CameraZoneId: new CameraZoneId("zone-1"),
                Cells: new List<GridCell>(),
                CreatedAt: DateTime.UtcNow, UpdatedAt: DateTime.UtcNow, OverlappingZoneIds: null),
        };

        var countBefore = regions.Count;
        MainWindow.BuildRegionListText(
            new CameraZoneId("zone-1"), "No regions", regions);

        Assert.Equal(countBefore, regions.Count);
    }
}
