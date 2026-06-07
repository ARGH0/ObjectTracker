using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ObjectTracker.UI.Desktop.ViewModels;

public partial class CameraWorkspaceViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isPaneOpen = true;

    [ObservableProperty]
    private bool _isPinned;

    [ObservableProperty]
    private double _openPaneLength = 340;

    [ObservableProperty]
    private double _compactPaneLength = 48;

    [ObservableProperty]
    private SplitViewDisplayMode _displayMode = SplitViewDisplayMode.CompactOverlay;

    [ObservableProperty]
    private string _toggleButtonText = ">";

    [ObservableProperty]
    private string _pinButtonText = "📌";

    [ObservableProperty]
    private bool _isPanelVisible = true;

    [ObservableProperty]
    private bool _isToggleButtonVisible = true;

    [ObservableProperty]
    private bool _isPinButtonVisible = true;

    [RelayCommand]
    private void TogglePane()
    {
        if (IsPinned)
        {
            return;
        }

        IsPaneOpen = !IsPaneOpen;
        RefreshLayout();
    }

    [RelayCommand]
    private void PinPane()
    {
        IsPinned = !IsPinned;
        IsPaneOpen = true;
        RefreshLayout();
    }

    public void ApplyLayout(bool isOpen, bool isPinned)
    {
        IsPaneOpen = isOpen;
        IsPinned = isPinned;
        RefreshLayout();
    }

    private void RefreshLayout()
    {
        if (IsPinned)
        {
            IsPaneOpen = true;
            DisplayMode = SplitViewDisplayMode.Inline;
            OpenPaneLength = 340;
            CompactPaneLength = 0;
            ToggleButtonText = string.Empty;
            PinButtonText = "📍";
            IsPanelVisible = true;
            IsToggleButtonVisible = false;
            IsPinButtonVisible = true;
            return;
        }

        DisplayMode = SplitViewDisplayMode.CompactOverlay;
        OpenPaneLength = 340;
        CompactPaneLength = 48;
        ToggleButtonText = IsPaneOpen ? "<" : ">";
        PinButtonText = "📌";
        IsPanelVisible = IsPaneOpen;
        IsToggleButtonVisible = true;
        IsPinButtonVisible = true;
    }
}
