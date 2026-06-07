using Avalonia.Controls;
using ObjectTracker.UI.Desktop;
using ObjectTracker.UI.Desktop.Enums;
using Xunit;

namespace ObjectTracker.UI.Desktop.Tests;

public sealed class MainWindowWorkspaceShellTests
{
    [Fact]
    public void BuildWorkspaceVisibility_Camera_ShowsOnlyCameraWorkspace()
    {
        var visibility = MainWindow.BuildWorkspaceVisibility(Workspace.Camera);

        Assert.True(visibility.CameraVisible);
        Assert.False(visibility.LayersVisible);
        Assert.False(visibility.SettingsVisible);
    }

    [Fact]
    public void BuildWorkspaceVisibility_Layers_ShowsOnlyLayersWorkspace()
    {
        var visibility = MainWindow.BuildWorkspaceVisibility(Workspace.Layers);

        Assert.False(visibility.CameraVisible);
        Assert.True(visibility.LayersVisible);
        Assert.False(visibility.SettingsVisible);
    }

    [Fact]
    public void BuildWorkspaceVisibility_Settings_ShowsOnlySettingsWorkspace()
    {
        var visibility = MainWindow.BuildWorkspaceVisibility(Workspace.Settings);

        Assert.False(visibility.CameraVisible);
        Assert.False(visibility.LayersVisible);
        Assert.True(visibility.SettingsVisible);
    }

    [Fact]
    public void WorkspaceNavigation_IsAllowed_DuringAmbiguityAlert()
    {
        Assert.True(MainWindow.IsWorkspaceNavigationAllowedDuringAmbiguity());
    }

    [Fact]
    public void RuntimeLog_IsVisible_OnlyInCameraWorkspace()
    {
        Assert.True(MainWindow.IsRuntimeLogVisibleForWorkspace(Workspace.Camera));
        Assert.False(MainWindow.IsRuntimeLogVisibleForWorkspace(Workspace.Layers));
        Assert.False(MainWindow.IsRuntimeLogVisibleForWorkspace(Workspace.Settings));
    }

    [Fact]
    public void BottomStatusSnapshot_ShowsRunningAndActiveAmbiguityStates()
    {
        var snapshot = MainWindow.BuildBottomStatusSnapshot(
            isVisionPipelineRunning: true,
            isAmbiguityActive: true,
            hasPendingVisionPipelineRestart: false);

        Assert.Equal("Vision Pipeline: running", snapshot.VisionPipeline);
        Assert.Equal("Ambiguity Alert: active", snapshot.AmbiguityAlert);
        Assert.Equal("Calibration: unknown", snapshot.Calibration);
        Assert.Equal("Pending restart: none", snapshot.PendingRestart);
    }

    [Fact]
    public void BottomStatusSnapshot_ShowsStoppedAndClearAmbiguityStates()
    {
        var snapshot = MainWindow.BuildBottomStatusSnapshot(
            isVisionPipelineRunning: false,
            isAmbiguityActive: false,
            hasPendingVisionPipelineRestart: false);

        Assert.Equal("Vision Pipeline: stopped", snapshot.VisionPipeline);
        Assert.Equal("Ambiguity Alert: clear", snapshot.AmbiguityAlert);
    }

    [Fact]
    public void BottomStatusSnapshot_ShowsPendingRestartWhenRequired()
    {
        var snapshot = MainWindow.BuildBottomStatusSnapshot(
            isVisionPipelineRunning: true,
            isAmbiguityActive: false,
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
    public void CameraListSelectionMode_IsSingle()
    {
        Assert.Equal(SelectionMode.Single, MainWindow.GetCameraListSelectionMode());
    }

    [Fact]
    public void CameraDestructiveActionsState_WhenVisionPipelineRunning_DisablesClearAndDelete()
    {
        var state = MainWindow.BuildCameraDestructiveActionsState(
            isVisionPipelineRunning: true,
            isAmbiguityActive: false,
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
            isAmbiguityActive: false,
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
            Workspace.Settings,
            Workspace.Camera,
            saved,
            draft,
            SettingsNavigationDecision.Cancel);

        Assert.Equal(Workspace.Settings, result.Workspace);
        Assert.Equal(saved, result.SavedSettings);
        Assert.Equal(draft, result.DraftSettings);
        Assert.True(result.HasUnsavedChanges);
    }

    [Theory]
    [InlineData(SettingsNavigationDecision.Save, 40, true)]
    [InlineData(SettingsNavigationDecision.Discard, 32, false)]
    public void SettingsNavigation_WithUnsavedChanges_AppliesSaveOrDiscard(
        SettingsNavigationDecision decision,
        int expectedSavedColumns,
        bool expectedPersist)
    {
        var saved = new AppSettings(GridColumns: 32, GridRows: 18);
        var draft = new AppSettings(GridColumns: 40, GridRows: 18);

        var result = MainWindow.ApplySettingsNavigationDecision(
            Workspace.Settings,
            Workspace.Camera,
            saved,
            draft,
            decision);

        Assert.Equal(Workspace.Camera, result.Workspace);
        Assert.Equal(expectedSavedColumns, result.SavedSettings.GridColumns);
        Assert.Equal(result.SavedSettings, result.DraftSettings);
        Assert.False(result.HasUnsavedChanges);
        Assert.Equal(expectedPersist, result.ShouldPersist);
    }

    [Theory]
    [InlineData(SettingsField.GridColumns)]
    [InlineData(SettingsField.GridRows)]
    public void SettingsApplyPolicyLabel_ForGridSettings_RequiresVisionPipelineRestart(SettingsField field)
    {
        var label = MainWindow.GetSettingsApplyPolicyLabel(field);

        Assert.Equal("requires Vision Pipeline restart", label);
    }

    [Fact]
    public void SettingsApplyPolicyLabel_ForMissingFrameBehavior_AppliesImmediately()
    {
        var label = MainWindow.GetSettingsApplyPolicyLabel(SettingsField.MissingFrameBehavior);

        Assert.Equal("applies immediately", label);
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
    public void SettingsSaveImpact_WhenOnlyMissingFrameBehaviorChangesWhileRunning_DoesNotMarkPendingRestart()
    {
        var saved = new AppSettings(GridColumns: 32, GridRows: 18, MissingFrameBehavior: CameraTileMissingFrameBehavior.RepeatLastFrame);
        var draft = new AppSettings(GridColumns: 32, GridRows: 18, MissingFrameBehavior: CameraTileMissingFrameBehavior.BlackFrame);

        var impact = MainWindow.BuildSettingsSaveImpact(saved, draft, isVisionPipelineRunning: true);

        Assert.False(impact.RequiresVisionPipelineRestart);
        Assert.False(impact.HasPendingVisionPipelineRestart);
        Assert.Equal("Settings: saved", impact.SettingsStatusText);
    }
}
