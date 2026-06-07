using Avalonia.Controls;
using ObjectTracker.UI.Desktop;
using ObjectTracker.UI.Desktop.Enums;
using Xunit;

namespace ObjectTracker.UI.Desktop.Tests;

public sealed class MainWindowWorkspaceShellTests
{
    /// <summary>
    /// <description>Feature: MainWindow.BuildWorkspaceVisibility shows only the selected workspace.
    /// 
    ///   Scenario: Building workspace visibility for Camera should show camera and hide layers and settings.
    ///     Given BuildWorkspaceVisibility(Workspace.Camera) is called,
    ///     Then CameraVisible should be true, LayersVisible should be false, SettingsVisible should be false.</description>
    /// </summary>
    [Fact]
    public void BuildWorkspaceVisibility_Camera_ShowsOnlyCameraWorkspace()
    {
        var visibility = MainWindow.BuildWorkspaceVisibility(Workspace.Camera);

        Assert.True(visibility.CameraVisible);
        Assert.False(visibility.LayersVisible);
        Assert.False(visibility.SettingsVisible);
    }

    /// <summary>
    /// <description>Feature: MainWindow.BuildWorkspaceVisibility shows only the selected workspace.
    /// 
    ///   Scenario: Building workspace visibility for Layers should show layers and hide camera and settings.
    ///     Given BuildWorkspaceVisibility(Workspace.Layers) is called,
    ///     Then CameraVisible should be false, LayersVisible should be true, SettingsVisible should be false.</description>
    /// </summary>
    [Fact]
    public void BuildWorkspaceVisibility_Layers_ShowsOnlyLayersWorkspace()
    {
        var visibility = MainWindow.BuildWorkspaceVisibility(Workspace.Layers);

        Assert.False(visibility.CameraVisible);
        Assert.True(visibility.LayersVisible);
        Assert.False(visibility.SettingsVisible);
    }

    /// <summary>
    /// <description>Feature: MainWindow.BuildWorkspaceVisibility shows only the selected workspace.
    /// 
    ///   Scenario: Building workspace visibility for Settings should show settings and hide camera and layers.
    ///     Given BuildWorkspaceVisibility(Workspace.Settings) is called,
    ///     Then CameraVisible should be false, LayersVisible should be false, SettingsVisible should be true.</description>
    /// </summary>
    [Fact]
    public void BuildWorkspaceVisibility_Settings_ShowsOnlySettingsWorkspace()
    {
        var visibility = MainWindow.BuildWorkspaceVisibility(Workspace.Settings);

        Assert.False(visibility.CameraVisible);
        Assert.False(visibility.LayersVisible);
        Assert.True(visibility.SettingsVisible);
    }

    /// <summary>
    /// <description>Feature: MainWindow workspace navigation is allowed during an Ambiguity Alert.
    /// 
    ///   Scenario: Checking whether workspace navigation is permitted while ambiguity is active should return true.
    ///     Given IsWorkspaceNavigationAllowedDuringAmbiguity() is called,
    ///     Then the result should be true.</description>
    /// </summary>
    [Fact]
    public void WorkspaceNavigation_IsAllowed_DuringAmbiguityAlert()
    {
        Assert.True(MainWindow.IsWorkspaceNavigationAllowedDuringAmbiguity());
    }

    /// <summary>
    /// <description>Feature: MainWindow runtime log visibility is restricted to the Camera workspace only.
    /// 
    ///   Scenario: Checking runtime log visibility for each workspace should return true only for Camera and false for Layers and Settings.
    ///     Given IsRuntimeLogVisibleForWorkspace is called for Camera, Layers, and Settings respectively,
    ///     Then Camera should return true, Layers should return false, Settings should return false.</description>
    /// </summary>
    [Fact]
    public void RuntimeLog_IsVisible_OnlyInCameraWorkspace()
    {
        Assert.True(MainWindow.IsRuntimeLogVisibleForWorkspace(Workspace.Camera));
        Assert.False(MainWindow.IsRuntimeLogVisibleForWorkspace(Workspace.Layers));
        Assert.False(MainWindow.IsRuntimeLogVisibleForWorkspace(Workspace.Settings));
    }

    /// <summary>
    /// <description>Feature: MainWindow.BuildBottomStatusSnapshot shows running Vision Pipeline and active Ambiguity Alert states.
    /// 
    ///   Scenario: Building a bottom status snapshot with the pipeline running, ambiguity active, and no pending restart should produce correct status text for all fields.
    ///     Given BuildBottomStatusSnapshot(isVisionPipelineRunning=true, isAmbiguityActive=true, hasPendingVisionPipelineRestart=false) is called,
    ///     Then VisionPipeline should be "Vision Pipeline: running", AmbiguityAlert should be "Ambiguity Alert: active", Calibration should be "Calibration: unknown", PendingRestart should be "Pending restart: none".</description>
    /// </summary>
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

    /// <summary>
    /// <description>Feature: MainWindow.BuildBottomStatusSnapshot shows stopped Vision Pipeline and clear Ambiguity Alert states.
    /// 
    ///   Scenario: Building a bottom status snapshot with the pipeline stopped, ambiguity clear, and no pending restart should produce correct status text for all fields.
    ///     Given BuildBottomStatusSnapshot(isVisionPipelineRunning=false, isAmbiguityActive=false, hasPendingVisionPipelineRestart=false) is called,
    ///     Then VisionPipeline should be "Vision Pipeline: stopped", AmbiguityAlert should be "Ambiguity Alert: clear".</description>
    /// </summary>
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

    /// <summary>
    /// <description>Feature: MainWindow.BuildBottomStatusSnapshot shows pending restart when required.
    /// 
    ///   Scenario: Building a bottom status snapshot with the pipeline running, ambiguity clear, and pending restart required should show "Pending restart: required".
    ///     Given BuildBottomStatusSnapshot(isVisionPipelineRunning=true, isAmbiguityActive=false, hasPendingVisionPipelineRestart=true) is called,
    ///     Then PendingRestart should be "Pending restart: required".</description>
    /// </summary>
    [Fact]
    public void BottomStatusSnapshot_ShowsPendingRestartWhenRequired()
    {
        var snapshot = MainWindow.BuildBottomStatusSnapshot(
            isVisionPipelineRunning: true,
            isAmbiguityActive: false,
            hasPendingVisionPipelineRestart: true);

        Assert.Equal("Pending restart: required", snapshot.PendingRestart);
    }

    /// <summary>
    /// <description>Feature: MainWindow.BuildVisionPipelineMenuState disables Start and enables Stop when the pipeline is running.
    /// 
    ///   Scenario: Building a menu state with isVisionPipelineRunning=true should produce disabled start and enabled stop buttons.
    ///     Given BuildVisionPipelineMenuState(isVisionPipelineRunning=true) is called,
    ///     Then StartEnabled should be false and StopEnabled should be true.</description>
    /// </summary>
    [Fact]
    public void VisionPipelineMenuState_WhenRunning_DisablesStartAndEnablesStop()
    {
        var state = MainWindow.BuildVisionPipelineMenuState(isVisionPipelineRunning: true);

        Assert.False(state.StartEnabled);
        Assert.True(state.StopEnabled);
    }

    /// <summary>
    /// <description>Feature: MainWindow.BuildVisionPipelineMenuState enables Start and disables Stop when the pipeline is stopped.
    /// 
    ///   Scenario: Building a menu state with isVisionPipelineRunning=false should produce enabled start and disabled stop buttons.
    ///     Given BuildVisionPipelineMenuState(isVisionPipelineRunning=false) is called,
    ///     Then StartEnabled should be true and StopEnabled should be false.</description>
    /// </summary>
    [Fact]
    public void VisionPipelineMenuState_WhenStopped_EnablesStartAndDisablesStop()
    {
        var state = MainWindow.BuildVisionPipelineMenuState(isVisionPipelineRunning: false);

        Assert.True(state.StartEnabled);
        Assert.False(state.StopEnabled);
    }

    /// <summary>
    /// <description>Feature: MainWindow camera list selection mode is single.
    /// 
    ///   Scenario: Getting the camera list selection mode should return Single.
    ///     Given GetCameraListSelectionMode() is called,
    ///     Then the result should be SelectionMode.Single.</description>
    /// </summary>
    [Fact]
    public void CameraListSelectionMode_IsSingle()
    {
        Assert.Equal(SelectionMode.Single, MainWindow.GetCameraListSelectionMode());
    }

    /// <summary>
    /// <description>Feature: MainWindow.BuildCameraDestructiveActionsState disables destructive actions when the Vision Pipeline is running.
    /// 
    ///   Scenario: Building a destructive actions state with isVisionPipelineRunning=true should disable delete and clear operations while requiring confirmation.
    ///     Given BuildCameraDestructiveActionsState(isVisionPipelineRunning=true, isAmbiguityActive=false, hasSelectedCamera=true, cameraCount=2) is called,
    ///     Then DeleteSelectedEnabled should be false, ClearAllEnabled should be false, DeleteSelectedRequiresConfirmation should be true, and ClearAllRequiresConfirmation should be true.</description>
    /// </summary>
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

    /// <summary>
    /// <description>Feature: MainWindow.BuildCameraDestructiveActionsState enables destructive actions when the Vision Pipeline is stopped and a selection exists.
    /// 
    ///   Scenario: Building a destructive actions state with isVisionPipelineRunning=false should enable delete and clear operations without requiring confirmation.
    ///     Given BuildCameraDestructiveActionsState(isVisionPipelineRunning=false, isAmbiguityActive=false, hasSelectedCamera=true, cameraCount=1) is called,
    ///     Then DeleteSelectedEnabled should be true and ClearAllEnabled should be true.</description>
    /// </summary>
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

    /// <summary>
    /// <description>Feature: MainWindow.BuildDeleteCameraConfirmationMessage uses the selected camera name in the confirmation dialog.
    /// 
    ///   Scenario: Building a delete confirmation message for "Camera A" should produce a question asking to delete that specific camera from the Session.
    ///     Given BuildDeleteCameraConfirmationMessage("Camera A") is called,
    ///     Then the result should be "Delete camera 'Camera A' from this Session?".</description>
    /// </summary>
    [Fact]
    public void DeleteCameraConfirmationMessage_UsesSelectedCameraName()
    {
        var message = MainWindow.BuildDeleteCameraConfirmationMessage("Camera A");

        Assert.Equal("Delete camera 'Camera A' from this Session?", message);
    }

    /// <summary>
    /// <description>Feature: MainWindow.BuildClearCamerasConfirmationMessage uses the camera count and a warning in the confirmation dialog.
    /// 
    ///   Scenario: Building a clear all confirmation message for 3 cameras should produce a message with the count and an irreversible action warning.
    ///     Given BuildClearCamerasConfirmationMessage(3) is called,
    ///     Then the result should be "Clear all 3 cameras from this Session? This cannot be undone.".</description>
    /// </summary>
    [Fact]
    public void ClearCamerasConfirmationMessage_UsesCameraCountAndWarning()
    {
        var message = MainWindow.BuildClearCamerasConfirmationMessage(3);

        Assert.Equal("Clear all 3 cameras from this Session? This cannot be undone.", message);
    }

    /// <summary>
    /// <description>Feature: MainWindow.BuildSettingsDraftState detects unsaved changes when draft differs from saved settings.
    /// 
    ///   Scenario: Building a settings draft state with different grid columns between saved (32) and draft (40) should show unsaved changes without modifying the original settings objects.
    ///     Given BuildSettingsDraftState is called with saved=AppSettings(32,18) and draft=AppSettings(40,18),
    ///     Then HasUnsavedChanges should be true, SavedSettings should equal the saved object (unchanged), DraftSettings should equal the draft object (unchanged), and StatusText should be "Settings: unsaved changes".</description>
    /// </summary>
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

    /// <summary>
    /// <description>Feature: MainWindow.ApplySettingsNavigationDecision with Cancel decision keeps the user in Settings workspace with draft intact.
    /// 
    ///   Scenario: Navigating away from Settings when there are unsaved changes and choosing to cancel should leave the user in Settings without changing saved or draft settings.
    ///     Given ApplySettingsNavigationDecision is called with Workspace.Settings -> Workspace.Camera, saved=AppSettings(32,18), draft=AppSettings(40,18), decision=Cancel,
    ///     Then result.Workspace should be Workspace.Settings (unchanged), SavedSettings should equal the original saved, DraftSettings should equal the original draft, and HasUnsavedChanges should remain true.</description>
    /// </summary>
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

    /// <summary>
    /// <description>Feature: MainWindow.ApplySettingsNavigationDecision applies save or discard of unsaved changes when navigating away from Settings.
    /// 
    ///   Scenario: Navigating away from Settings with unsaved changes and choosing to Save should persist the draft as saved, navigate to Camera workspace, and set ShouldPersist=true. Choosing Discard should revert saved back to original values, navigate to Camera workspace, and set ShouldPersist=false.
    ///     Given ApplySettingsNavigationDecision is called with Workspace.Settings -> Workspace.Camera, saved=AppSettings(32,18), draft=AppSettings(40,18), for both Save and Discard decisions,
    ///     Then for Save: result.Workspace should be Camera, SavedSettings.GridColumns should be 40 (draft value), DraftSettings should equal the new saved settings, HasUnsavedChanges should be false, ShouldPersist should be true. For Discard: result.Workspace should be Camera, SavedSettings.GridColumns should be 32 (original saved), HasUnsavedChanges should be false, ShouldPersist should be false.</description>
    /// </summary>
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

    /// <summary>
    /// <description>Feature: MainWindow.GetSettingsApplyPolicyLabel returns "requires Vision Pipeline restart" for grid-related settings.
    /// 
    ///   Scenario: Getting the apply policy label for GridColumns and GridRows fields should return a string indicating that a Vision Pipeline restart is required.
    ///     Given GetSettingsApplyPolicyLabel is called for SettingsField.GridColumns and SettingsField.GridRows,
    ///     Then both results should be "requires Vision Pipeline restart".</description>
    /// </summary>
    [Theory]
    [InlineData(SettingsField.GridColumns)]
    [InlineData(SettingsField.GridRows)]
    public void SettingsApplyPolicyLabel_ForGridSettings_RequiresVisionPipelineRestart(SettingsField field)
    {
        var label = MainWindow.GetSettingsApplyPolicyLabel(field);

        Assert.Equal("requires Vision Pipeline restart", label);
    }

    /// <summary>
    /// <description>Feature: MainWindow.GetSettingsApplyPolicyLabel returns "applies immediately" for MissingFrameBehavior.
    /// 
    ///   Scenario: Getting the apply policy label for SettingsField.MissingFrameBehavior should return a string indicating immediate application without restart.
    ///     Given GetSettingsApplyPolicyLabel(SettingsField.MissingFrameBehavior) is called,
    ///     Then the result should be "applies immediately".</description>
    /// </summary>
    [Fact]
    public void SettingsApplyPolicyLabel_ForMissingFrameBehavior_AppliesImmediately()
    {
        var label = MainWindow.GetSettingsApplyPolicyLabel(SettingsField.MissingFrameBehavior);

        Assert.Equal("applies immediately", label);
    }

    /// <summary>
    /// <description>Feature: MainWindow.BuildSettingsSaveImpact marks pending restart when grid settings change while the Vision Pipeline is running.
    /// 
    ///   Scenario: Building a settings save impact with saved=AppSettings(32,18) and draft=AppSettings(40,18) while isVisionPipelineRunning=true should indicate that a restart is required and pending.
    ///     Given BuildSettingsSaveImpact(saved=AppSettings(32,18), draft=AppSettings(40,18), isVisionPipelineRunning=true) is called,
    ///     Then RequiresVisionPipelineRestart should be true, HasPendingVisionPipelineRestart should be true, and SettingsStatusText should be "Settings: saved, pending Vision Pipeline restart".</description>
    /// </summary>
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

    /// <summary>
    /// <description>Feature: MainWindow.BuildSettingsSaveImpact does not mark pending restart when only MissingFrameBehavior changes while the Vision Pipeline is running.
    /// 
    ///   Scenario: Building a settings save impact with saved=AppSettings(32,18,RepeatLastFrame) and draft=AppSettings(32,18,BlackFrame) while isVisionPipelineRunning=true should not require a restart since MissingFrameBehavior applies immediately.
    ///     Given BuildSettingsSaveImpact(saved=AppSettings(32,18,MissingFrameBehavior:RepeatLastFrame), draft=AppSettings(32,18,MissingFrameBehavior:BlackFrame), isVisionPipelineRunning=true) is called,
    ///     Then RequiresVisionPipelineRestart should be false, HasPendingVisionPipelineRestart should be false, and SettingsStatusText should be "Settings: saved".</description>
    /// </summary>
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
