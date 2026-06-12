using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ObjectTracker.UI.Desktop;

internal readonly record struct UsbCameraDevice(
    string Id,
    string DisplayName,
    string HardwareName,
    int DeviceIndex,
    string? DevicePath);

internal enum UsbCameraOperatingSystem
{
    Linux,
    Windows,
    Unsupported
}

internal static class UsbCameraDeviceEnumerator
{
    public static IReadOnlyList<UsbCameraDevice> Enumerate(
        UsbCameraOperatingSystem? operatingSystem = null,
        string linuxDevRoot = "/dev",
        string linuxSysVideoRoot = "/sys/class/video4linux")
    {
        var current = operatingSystem ?? GetCurrentOperatingSystem();
        return current switch
        {
            UsbCameraOperatingSystem.Linux => EnumerateLinuxDevices(linuxDevRoot, linuxSysVideoRoot),
            _ => Array.Empty<UsbCameraDevice>()
        };
    }

    public static IReadOnlyList<UsbCameraDevice> EnumerateLinuxDevices(string devRoot = "/dev", string sysVideoRoot = "/sys/class/video4linux")
    {
        if (!Directory.Exists(devRoot))
        {
            return Array.Empty<UsbCameraDevice>();
        }

        var devices = Directory
            .EnumerateFiles(devRoot, "video*")
            .Select(path => BuildLinuxDevice(path, sysVideoRoot))
            .Where(device => device.HasValue)
            .Select(device => device!.Value)
            .OrderBy(device => device.DeviceIndex)
            .ThenBy(device => device.DevicePath, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return WithOperatorDisplayNames(devices);
    }

    public static IReadOnlyList<UsbCameraDevice> WithOperatorDisplayNames(IReadOnlyList<UsbCameraDevice> devices)
    {
        var duplicateNames = devices
            .GroupBy(device => device.HardwareName, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var duplicateIndexesByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var named = new List<UsbCameraDevice>(devices.Count);

        foreach (var device in devices)
        {
            if (!duplicateNames.Contains(device.HardwareName))
            {
                named.Add(device with { DisplayName = device.HardwareName });
                continue;
            }

            duplicateIndexesByName.TryGetValue(device.HardwareName, out var index);
            index++;
            duplicateIndexesByName[device.HardwareName] = index;
            named.Add(device with { DisplayName = $"{device.HardwareName} {index}" });
        }

        return named;
    }

    private static UsbCameraDevice? BuildLinuxDevice(string devPath, string sysVideoRoot)
    {
        var devFileName = Path.GetFileName(devPath);
        if (!TryParseLinuxDeviceIndex(devFileName, out var deviceIndex))
        {
            return null;
        }

        var devicePath = $"/dev/{devFileName}";
        var hardwareName = ReadLinuxHardwareName(sysVideoRoot, devFileName);
        return new UsbCameraDevice(
            $"usb:{devicePath}",
            hardwareName,
            hardwareName,
            deviceIndex,
            devicePath);
    }

    private static bool TryParseLinuxDeviceIndex(string devFileName, out int deviceIndex)
    {
        const string prefix = "video";
        deviceIndex = 0;
        return devFileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            && int.TryParse(devFileName[prefix.Length..], out deviceIndex);
    }

    private static string ReadLinuxHardwareName(string sysVideoRoot, string devFileName)
    {
        var namePath = Path.Combine(sysVideoRoot, devFileName, "name");
        if (!File.Exists(namePath))
        {
            return devFileName;
        }

        var name = File.ReadAllText(namePath).Trim();
        return string.IsNullOrWhiteSpace(name) ? devFileName : name;
    }

    private static UsbCameraOperatingSystem GetCurrentOperatingSystem()
    {
        if (OperatingSystem.IsLinux())
        {
            return UsbCameraOperatingSystem.Linux;
        }

        if (OperatingSystem.IsWindows())
        {
            return UsbCameraOperatingSystem.Windows;
        }

        return UsbCameraOperatingSystem.Unsupported;
    }
}
