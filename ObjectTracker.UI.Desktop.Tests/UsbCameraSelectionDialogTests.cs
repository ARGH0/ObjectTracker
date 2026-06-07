using ObjectTracker.UI.Desktop;
using OpenCvSharp;
using Xunit;

namespace ObjectTracker.UI.Desktop.Tests;

public sealed class UsbCameraSelectionDialogTests
{
    /// <summary>
    /// <description>Feature: UsbCameraSelectionPolicy.ResolveSelectableOption cannot confirm a disabled already-added option.
    /// 
    ///   Scenario: Resolving a selectable option with selectedIndex=0 pointing to an unavailable, "Already added" USB camera should return null.
    ///     Given ResolveSelectableOption is called with one option (usb:0:ANY, IsAvailable=false, Status="Already added") and selectedIndex=0,
    ///     Then the result should be null.</description>
    /// </summary>
    [Fact]
    public void ResolveSelectableOption_DisabledAlreadyAddedOption_CannotBeConfirmed()
    {
        var options = new[]
        {
            new UsbCameraOption("usb:0:ANY", "USB camera 0 (already added)", 0, VideoCaptureAPIs.ANY, IsAvailable: false, Status: "Already added")
        };

        var selected = UsbCameraSelectionPolicy.ResolveSelectableOption(options, selectedIndex: 0);

        Assert.Null(selected);
    }

    /// <summary>
    /// <description>Feature: UsbCameraSelectionPolicy.ResolveSelectableOption can confirm an enabled option.
    /// 
    ///   Scenario: Resolving a selectable option with selectedIndex=0 pointing to an available USB camera should return that option's ID.
    ///     Given ResolveSelectableOption is called with one option (usb:0:ANY, IsAvailable=true) and selectedIndex=0,
    ///     Then the result Id should be "usb:0:ANY".</description>
    /// </summary>
    [Fact]
    public void ResolveSelectableOption_EnabledOption_CanBeConfirmed()
    {
        var options = new[]
        {
            new UsbCameraOption("usb:0:ANY", "USB camera 0", 0, VideoCaptureAPIs.ANY)
        };

        var selected = UsbCameraSelectionPolicy.ResolveSelectableOption(options, selectedIndex: 0);

        Assert.Equal("usb:0:ANY", selected?.Id);
    }
}
