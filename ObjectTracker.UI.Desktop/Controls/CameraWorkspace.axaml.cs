using System;
using Avalonia.Controls;
using ObjectTracker.UI.Desktop.ViewModels;

namespace ObjectTracker.UI.Desktop;

public partial class CameraWorkspace : UserControl
{
    public event EventHandler? AddCameraRequested;
    public event EventHandler? RemoveCameraRequested;
    public event EventHandler? PreviousVideoRequested;
    public event EventHandler? NextVideoRequested;
    public event EventHandler? MarkAmbiguityRequested;
    public event EventHandler? ResolveRemovedRequested;
    public event EventHandler? ResolveRelinkRequested;
    public event EventHandler? ResolveFalseRequested;
    public event EventHandler? ResolveOtherRequested;
    public event EventHandler? ToggleGridEditorRequested;
    public event EventHandler? RefreshCompositionPreviewRequested;

    public CameraWorkspace()
    {
        InitializeComponent();
        DataContext = new CameraWorkspaceViewModel();
        HookEvents();
    }

    private void HookEvents()
    {
        AddVideosButton.Click += (_, _) => AddCameraRequested?.Invoke(this, EventArgs.Empty);
        RemoveSelectedButton.Click += (_, _) => RemoveCameraRequested?.Invoke(this, EventArgs.Empty);
        PreviousVideoButton.Click += (_, _) => PreviousVideoRequested?.Invoke(this, EventArgs.Empty);
        NextVideoButton.Click += (_, _) => NextVideoRequested?.Invoke(this, EventArgs.Empty);
        MarkAmbiguityButton.Click += (_, _) => MarkAmbiguityRequested?.Invoke(this, EventArgs.Empty);
        ResolveRemovedButton.Click += (_, _) => ResolveRemovedRequested?.Invoke(this, EventArgs.Empty);
        ResolveRelinkButton.Click += (_, _) => ResolveRelinkRequested?.Invoke(this, EventArgs.Empty);
        ResolveFalseButton.Click += (_, _) => ResolveFalseRequested?.Invoke(this, EventArgs.Empty);
        ResolveOtherButton.Click += (_, _) => ResolveOtherRequested?.Invoke(this, EventArgs.Empty);
        ToggleGridEditorButton.Click += (_, _) => ToggleGridEditorRequested?.Invoke(this, EventArgs.Empty);
        RefreshCompositionPreviewButton.Click += (_, _) => RefreshCompositionPreviewRequested?.Invoke(this, EventArgs.Empty);
    }

    public void ApplyCameraPanelLayout(bool isOpen, bool isPinned)
    {
        if (DataContext is CameraWorkspaceViewModel viewModel)
        {
            viewModel.ApplyLayout(isOpen, isPinned);
        }
    }

    public void SetStatus(string text)
    {
        StatusText.Text = text;
    }

    public void SetCurrentVideo(string text)
    {
        CurrentVideoText.Text = text;
    }

    public void SetAmbiguityBanner(bool isVisible, string text)
    {
        AmbiguityBanner.IsVisible = isVisible;
        AmbiguityText.Text = text;
    }
}
