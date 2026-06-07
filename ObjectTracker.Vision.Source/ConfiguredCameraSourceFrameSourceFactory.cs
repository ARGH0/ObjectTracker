using ObjectTracker.Core.Domain;
using ObjectTracker.Core.Ports;

namespace ObjectTracker.Vision.Source;

public enum ConfiguredCameraSourceKind
{
    Usb,
    File
}

public sealed record ConfiguredCameraSource(
    string Id,
    string DisplayName,
    ConfiguredCameraSourceKind Kind,
    UsbCameraKey? UsbKey,
    UsbCaptureSettings UsbSettings,
    string? VideoPath,
    bool LoopVideo)
{
    public static ConfiguredCameraSource Usb(string id, string displayName, UsbCameraKey key, UsbCaptureSettings settings)
    {
        return new ConfiguredCameraSource(id, displayName, ConfiguredCameraSourceKind.Usb, key, settings, null, false);
    }

    public static ConfiguredCameraSource File(string id, string displayName, string videoPath, bool loopVideo)
    {
        return new ConfiguredCameraSource(id, displayName, ConfiguredCameraSourceKind.File, null, UsbCaptureSettings.Default, videoPath, loopVideo);
    }
}

public sealed class ConfiguredCameraSourceFrameSourceFactory : IFrameSourceFactory
{
    private readonly Dictionary<string, ConfiguredCameraSource> sourcesById;
    private readonly UsbCameraOwnerManager usbManager;
    private readonly FileCameraSourceFeedManager fileManager;

    public ConfiguredCameraSourceFrameSourceFactory(
        IEnumerable<ConfiguredCameraSource> sources,
        UsbCameraOwnerManager usbManager,
        FileCameraSourceFeedManager fileManager)
    {
        sourcesById = sources.ToDictionary(source => source.Id, StringComparer.OrdinalIgnoreCase);
        this.usbManager = usbManager;
        this.fileManager = fileManager;
    }

    public IReadOnlyList<FrameSourceInfo> GetAvailableSources()
    {
        return sourcesById.Values
            .Select(source => new FrameSourceInfo(source.Id, source.DisplayName))
            .OrderBy(source => source.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public IFrameSource Create(string sourceId)
    {
        if (!sourcesById.TryGetValue(sourceId, out var source))
        {
            throw new InvalidOperationException($"Unknown Camera Source '{sourceId}'.");
        }

        return source.Kind == ConfiguredCameraSourceKind.Usb
            ? new SessionOwnedUsbFrameSource(source, usbManager)
            : new SessionOwnedFileFrameSource(source, fileManager);
    }

    private sealed class SessionOwnedUsbFrameSource(ConfiguredCameraSource source, UsbCameraOwnerManager manager) : IFrameSource
    {
        private const int FrameWaitTimeoutMs = 250;
        private UsbCameraLease? lease;
        private long previousFrameVersion;

        public string Id => source.Id;

        public string DisplayName => source.DisplayName;

        public string Diagnostics { get; private set; } = "USB Camera Source feed not started";

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var key = source.UsbKey ?? throw new InvalidOperationException($"USB Camera Source '{source.Id}' has no USB key.");
            lease = await manager.AcquireAsync(key, source.UsbSettings, cancellationToken);
            Diagnostics = $"source={source.Id} | session-owned USB feed";
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (lease is not null)
            {
                await lease.DisposeAsync();
                lease = null;
            }
        }

        public async Task<FramePacket?> ReadFrameAsync(CancellationToken cancellationToken)
        {
            if (lease is null)
            {
                return null;
            }

            var frame = await lease.WaitForNextFrameAsync(previousFrameVersion, TimeSpan.FromMilliseconds(FrameWaitTimeoutMs), cancellationToken);
            if (frame is null)
            {
                return null;
            }

            previousFrameVersion = frame.Value.FrameVersion;
            return new FramePacket(source.Id, frame.Value.TimestampUtcMs, frame.Value.Width, frame.Value.Height, frame.Value.EncodedJpeg);
        }

        public string? ConsumeDiagnosticEvent() => null;

        public async ValueTask DisposeAsync()
        {
            await StopAsync(CancellationToken.None);
        }
    }

    private sealed class SessionOwnedFileFrameSource(ConfiguredCameraSource source, FileCameraSourceFeedManager manager) : IFrameSource
    {
        private const int FrameWaitTimeoutMs = 250;
        private FileCameraSourceFeedLease? lease;
        private long previousFrameVersion;

        public string Id => source.Id;

        public string DisplayName => source.DisplayName;

        public string Diagnostics { get; private set; } = "File Camera Source feed not started";

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(source.VideoPath))
            {
                throw new InvalidOperationException($"File Camera Source '{source.Id}' has no video path.");
            }

            lease = await manager.AcquireAsync(
                new FileCameraSourceKey(source.Id, source.VideoPath),
                new FileCameraSourcePlaybackSettings(source.LoopVideo),
                cancellationToken);
            Diagnostics = $"source={source.Id} | session-owned file feed";
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (lease is not null)
            {
                await lease.DisposeAsync();
                lease = null;
            }
        }

        public async Task<FramePacket?> ReadFrameAsync(CancellationToken cancellationToken)
        {
            if (lease is null)
            {
                return null;
            }

            var frame = await lease.WaitForNextFrameAsync(previousFrameVersion, TimeSpan.FromMilliseconds(FrameWaitTimeoutMs), cancellationToken);
            if (frame is null)
            {
                return null;
            }

            previousFrameVersion = frame.Value.FrameVersion;
            return new FramePacket(source.Id, frame.Value.TimestampUtcMs, frame.Value.Width, frame.Value.Height, frame.Value.EncodedJpeg);
        }

        public string? ConsumeDiagnosticEvent() => null;

        public async ValueTask DisposeAsync()
        {
            await StopAsync(CancellationToken.None);
        }
    }
}
