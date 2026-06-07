using ObjectTracker.Core.Ports;
using ObjectTracker.Vision.Source;
using Xunit;

namespace ObjectTracker.Vision.Tests;

public sealed class ConfiguredCameraSourceFrameSourceFactoryTests
{
    [Fact]
    public async Task Create_ForConfiguredUsbAndFileCameraSources_ReadsFramesThroughSessionOwnedFeeds()
    {
        var usbBackend = new FakeUsbCaptureBackend();
        var fileBackend = new FakeFileCameraSourcePlaybackBackend();
        await using var usbManager = new UsbCameraOwnerManager(usbBackend);
        await using var fileManager = new FileCameraSourceFeedManager(fileBackend);
        var usbKey = new UsbCameraKey(0, "ANY");
        var fileKey = new FileCameraSourceKey("file-bridge", "/videos/bridge.mp4");
        IFrameSourceFactory factory = new ConfiguredCameraSourceFrameSourceFactory(
            [
                ConfiguredCameraSource.Usb("usb:0:ANY", "USB Yard", usbKey, UsbCaptureSettings.Default),
                ConfiguredCameraSource.File("file-bridge", "Bridge File", fileKey.VideoPath, loopVideo: true)
            ],
            usbManager,
            fileManager);

        await using var usbSource = factory.Create("usb:0:ANY");
        await using var fileSource = factory.Create("file-bridge");

        await usbSource.StartAsync(CancellationToken.None);
        await fileSource.StartAsync(CancellationToken.None);
        var usbFrame = await usbSource.ReadFrameAsync(CancellationToken.None);
        var fileFrame = await fileSource.ReadFrameAsync(CancellationToken.None);
        var repeatedUsbFrame = await usbSource.ReadFrameAsync(CancellationToken.None);
        var repeatedFileFrame = await fileSource.ReadFrameAsync(CancellationToken.None);
        await usbSource.StopAsync(CancellationToken.None);
        await fileSource.StopAsync(CancellationToken.None);

        Assert.Equal(new[] { "file-bridge", "usb:0:ANY" }, factory.GetAvailableSources().Select(source => source.Id).Order().ToArray());
        Assert.Equal(1, usbBackend.GetOpenCount(usbKey));
        Assert.Equal(1, fileBackend.GetOpenCount(fileKey));
        Assert.Equal("usb:0:ANY", usbFrame?.SourceId);
        Assert.Equal("file-bridge", fileFrame?.SourceId);
        Assert.Null(repeatedUsbFrame);
        Assert.Null(repeatedFileFrame);
        Assert.Equal("USB Yard", usbSource.DisplayName);
        Assert.Equal("Bridge File", fileSource.DisplayName);
    }

    private sealed class FakeUsbCaptureBackend : IUsbCaptureBackend
    {
        private readonly Dictionary<UsbCameraKey, int> openCounts = new();

        public int GetOpenCount(UsbCameraKey key) => openCounts.TryGetValue(key, out var count) ? count : 0;

        public ValueTask<IUsbCaptureSession> OpenAsync(UsbCameraKey key, UsbCaptureSettings settings, CancellationToken cancellationToken)
        {
            openCounts[key] = GetOpenCount(key) + 1;
            return ValueTask.FromResult<IUsbCaptureSession>(new FakeUsbCaptureSession(key));
        }
    }

    private sealed class FakeUsbCaptureSession(UsbCameraKey key) : IUsbCaptureSession
    {
        private bool returnedFrame;

        public ValueTask<UsbCapturedFrame?> ReadFrameAsync(CancellationToken cancellationToken)
        {
            if (returnedFrame)
            {
                return ValueTask.FromResult<UsbCapturedFrame?>(null);
            }

            returnedFrame = true;
            return ValueTask.FromResult<UsbCapturedFrame?>(new UsbCapturedFrame(
                UsbCameraSourceId.Format(key),
                TimestampUtcMs: 1,
                Width: 2,
                Height: 2,
                EncodedJpeg: [1, 2, 3]));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeFileCameraSourcePlaybackBackend : IFileCameraSourcePlaybackBackend
    {
        private readonly Dictionary<FileCameraSourceKey, int> openCounts = new();

        public int GetOpenCount(FileCameraSourceKey key) => openCounts.TryGetValue(key, out var count) ? count : 0;

        public ValueTask<IFileCameraSourcePlaybackSession> OpenAsync(
            FileCameraSourceKey key,
            FileCameraSourcePlaybackSettings settings,
            CancellationToken cancellationToken)
        {
            openCounts[key] = GetOpenCount(key) + 1;
            return ValueTask.FromResult<IFileCameraSourcePlaybackSession>(new FakeFileCameraSourcePlaybackSession(key));
        }
    }

    private sealed class FakeFileCameraSourcePlaybackSession(FileCameraSourceKey key) : IFileCameraSourcePlaybackSession
    {
        private bool returnedFrame;

        public ValueTask<FileCameraSourceFrame?> ReadFrameAsync(CancellationToken cancellationToken)
        {
            if (returnedFrame)
            {
                return ValueTask.FromResult<FileCameraSourceFrame?>(null);
            }

            returnedFrame = true;
            return ValueTask.FromResult<FileCameraSourceFrame?>(new FileCameraSourceFrame(
                key.CameraId,
                TimestampUtcMs: 1,
                Width: 2,
                Height: 2,
                EncodedJpeg: [4, 5, 6]));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
