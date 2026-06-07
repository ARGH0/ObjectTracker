using System.Collections.Generic;

namespace ObjectTracker.UI.Desktop;

public static class UsbCameraSelectionPolicy
{
    public static UsbCameraOption? ResolveSelectableOption(IReadOnlyList<UsbCameraOption> options, int selectedIndex)
    {
        if (selectedIndex < 0 || selectedIndex >= options.Count)
        {
            return null;
        }

        var option = options[selectedIndex];
        return option.IsAvailable ? option : null;
    }
}
