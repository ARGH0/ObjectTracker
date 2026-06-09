using System.Threading.Tasks;

namespace ObjectTracker.UI.Desktop;

public static class UsbCameraOwnerManagerExtensions
{
    private static readonly UsbCameraOwnerManager SharedManager = new(new OpenCvUsbCaptureBackend());

    public static async Task<UsbCameraLease> AcquireOnceAsync(UsbCameraKey key, UsbCaptureSettings settings)
    {
        return await SharedManager.AcquireAsync(key, settings, default);
    }
}
