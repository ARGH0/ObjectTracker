using CommunityToolkit.Mvvm.ComponentModel;

namespace ObjectTracker.UI.Desktop.ViewModels;

public partial class LayerWorkspaceViewModel : ObservableObject
{
    [ObservableProperty]
    private string _deleteStatus = "Select a Layer Type to inspect usage.";
}
