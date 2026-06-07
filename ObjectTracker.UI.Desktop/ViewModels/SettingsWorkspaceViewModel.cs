using CommunityToolkit.Mvvm.ComponentModel;

namespace ObjectTracker.UI.Desktop.ViewModels;

public partial class SettingsWorkspaceViewModel : ObservableObject
{
    [ObservableProperty]
    private string _gridColumnsPolicy = "requires Vision Pipeline restart";

    [ObservableProperty]
    private string _gridRowsPolicy = "requires Vision Pipeline restart";

    [ObservableProperty]
    private string _missingFrameBehaviorPolicy = "applies immediately";

    [ObservableProperty]
    private string _draftStatus = "Settings: saved";

    [ObservableProperty]
    private string _pendingRestartStatus = "Pending restart: none";

    [ObservableProperty]
    private bool _canSave;

    [ObservableProperty]
    private bool _canDiscard;
}
