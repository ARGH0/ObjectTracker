using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

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
            UsbCameraOperatingSystem.Windows => EnumerateWindowsDevices(),
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

    private static IReadOnlyList<UsbCameraDevice> EnumerateWindowsDevices()
    {
        if (!OperatingSystem.IsWindows())
        {
            return Array.Empty<UsbCameraDevice>();
        }

        try
        {
            var devices = new List<UsbCameraDevice>();
            var devEnum = (ICreateDevEnum)Activator.CreateInstance(Type.GetTypeFromCLSID(CLSID_SystemDeviceEnum)!);

            IEnumMoniker? enumerator = null;
            try
            {
                devEnum.CreateClassEnumerator(FilterCategory, out enumerator, 0);
                if (enumerator is null)
                {
                    return Array.Empty<UsbCameraDevice>();
                }

                int fetched = 0;
                while (enumerator.Next(1, out var monikerArray, out fetched) == 0 && fetched > 0)
                {
                    var moniker = monikerArray[0];
                    try
                    {
                        if (BindingToPropertyBag(moniker, out var props))
                        {
                            var friendlyName = props["FriendlyName"]?.ToString() ?? "Unknown Device";
                            var devicePath = props["DevicePath"]?.ToString() ?? string.Empty;

                            devices.Add(new UsbCameraDevice(
                                $"usb:{devicePath}",
                                friendlyName,
                                friendlyName,
                                devices.Count,
                                devicePath));
                        }
                    }
                    finally
                    {
                        Marshal.ReleaseComObject(moniker);
                    }
                }
            }
            finally
            {
                if (enumerator is not null)
                {
                    Marshal.ReleaseComObject(enumerator);
                }

                Marshal.ReleaseComObject(devEnum);
            }

            return WithOperatorDisplayNames(devices.OrderBy(d => d.DeviceIndex).ToList());
        }
        catch
        {
            return Array.Empty<UsbCameraDevice>();
        }
    }

    private static bool BindingToPropertyBag(IMoniker moniker, out Dictionary<string, object?> props)
    {
        props = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        IPropertyBag? propertyBag = null;
        try
        {
            var result = moniker.BindToStorage(null, null, typeof(IPropertyBag).GUID, out propertyBag);
            if (result != 0)
            {
                return false;
            }

            var keys = new[] { "FriendlyName", "DevicePath", "Description" };
            foreach (var key in keys)
            {
                try
                {
                    var variant = new Variant();
                    propertyBag.Read(key, ref variant, IntPtr.Zero);
                    props[key] = variant.Value;
                }
                catch
                {
                    // Skip keys that don't exist
                }
            }

            return true;
        }
        finally
        {
            if (propertyBag is not null)
            {
                Marshal.ReleaseComObject(propertyBag);
            }
        }
    }

    [ComImport]
    [Guid("62BE5D10-60EB-11D0-BD5B-0000F804FA58")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ICreateDevEnum
    {
        [PreserveSig]
        int CreateClassEnumerator([In] ref Guid type, [Out, MarshalAs(UnmanagedType.IUnknown)] out IEnumMoniker pEnum, [In] int dwFlags);
    }

    [ComImport]
    [Guid("56CEBD92-00BD-11CF-A5DB-00AA006C0768")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyBag
    {
        [PreserveSig]
        int Read([In, MarshalAs(UnmanagedType.LPWStr)] string pszPropName, [In, Out] ref Variant pVar, [In] IntPtr pErrorLog);

        [PreserveSig]
        int Write([In, MarshalAs(UnmanagedType.LPWStr)] string pszPropName, [In] ref Variant pVar);
    }

    [ComImport]
    [Guid("0000000F-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMoniker
    {
        [PreserveSig]
        int BindToStorage([In] object prgfnToBind, [In] object ppbk, [In] ref Guid riid, [Out, MarshalAs(UnmanagedType.IUnknown)] out IPropertyBag ppStorage);

        [PreserveSig]
        int BindToObject([In] IStream pIStream, [In] object prgfnToBind, [In] ref Guid riid, [Out, MarshalAs(UnmanagedType.IUnknown)] out object ppv);

        [PreserveSig]
        int BindAsFile([In] IStream pStm, [Out, MarshalAs(UnmanagedType.LPWStr)] out string pszName);

        [PreserveSig]
        int GetTimeOfLastChange([In] IntPtr hwnd, [In, Out] ref FILETIME pFileTime);

        [PreserveSig]
        int IsEqual(IMoniker pmonikerFull);

        [PreserveSig]
        int Reduce([In] object prgfnToLeft, [In] int dwHowFar, [Out, MarshalAs(UnmanagedType.IUnknown)] out IMoniker pmonikerReduced, [In, Out] ref bool pfFullyResolved);

        [PreserveSig]
        int GetDisplayName([In] object pbc, [In] object prgfnToLeft, [Out, MarshalAs(UnmanagedType.LPWStr)] out string pszName);

        [PreserveSig]
        int GetClassID([Out] out Guid pClassId);

        [PreserveSig]
        int IsDirty();

        [PreserveSig]
        int IsRunning([In] object pbc, [In] object pbcNew, [In] object pmkNew);

        [PreserveSig]
        int GetLastWriteTime([In] object pbc, [In, Out] ref FILETIME pFileTime);

        [PreserveSig]
        int FasterThan([In] object pbc, [In] object pmkOther, [In] int dwForwardingFlags, [Out] out bool pfFaster);

        [PreserveSig]
        int GetSplitName([In] object pbc, [Out] IntPtr ppmidLeft, [Out, MarshalAs(UnmanagedType.LPWStr)] out string pszDisplayName);

        [PreserveSig]
        int BindToObject2([In] object pbc, [In] object prgfnToLeft, [In] ref Guid riid, [Out, MarshalAs(UnmanagedType.IUnknown)] out object ppv);
    }

    [ComImport]
    [Guid("00000015-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IEnumMoniker
    {
        [PreserveSig]
        int Next([In] int celt, [Out, MarshalAs(UnmanagedType.LPArray)] out IMoniker[] rgelt, [Out] out int pceltFetched);

        [PreserveSig]
        int Skip([In] int celt);

        [PreserveSig]
        int Reset();

        [PreserveSig]
        int Clone([Out, MarshalAs(UnmanagedType.IUnknown)] out IEnumMoniker ppenum);
    }

    private struct Variant
    {
        public short vt;
        public short wReserved1;
        public short wReserved2;
        public short wReserved3;
        public IntPtr value;
        public byte bVal;
        public byte iVal;
        public byte uiVal;
        public byte lVal;
        public byte ulVal;
        public byte hVal;
        public byte uhVal;
        public byte resultHResult;
        public byte resultPointer;

        public object? Value => vt switch
        {
            30 => Marshal.PtrToStringBSTR(value), // VT_BSTR
            _ => null
        };
    }

    private static readonly Guid CLSID_SystemDeviceEnum = new("88D6E80A-F1B2-11D4-9A73-00A0C91313F2");
    private static readonly Guid FilterCategory = new("860BB310-5D01-11d0-BD3B-00A0C911CE86");
}
