using ObjectTracker.Vision.Source;
using System.Runtime.ExceptionServices;
using Xunit;
using Cv = OpenCvSharp;

namespace ObjectTracker.Vision.Tests;

public sealed class UsbCameraOwnerManagerTests
{
    /// <summary>
    /// <description>Feature: UsbCameraOwnerManager shares one physical capture owner between multiple leases for the same camera key.
    /// 
    ///   Scenario: Two leases for the same USB camera source share a single backend open and produce identical frame versions.
    ///     Given a UsbCameraOwnerManager backed by FakeUsbCaptureBackend with one camera key (usb:0:ANY),
    ///      And a first lease acquired from the manager,
    ///      And a second lease acquired from the manager for the same key,
    ///     When both leases wait for their next frame,
    ///     Then the backend open count should be exactly 1,
    ///      And both frames should be non-null,
    ///      And both frames should have identical FrameVersion values.</description>
    /// </summary>
    [Fact]
    public async Task AcquireAsync_SameCameraSourceTwice_UsesOnePhysicalCaptureOwner()
    {
        var backend = new FakeUsbCaptureBackend();
        await using var manager = new UsbCameraOwnerManager(backend);
        var key = new UsbCameraKey(0, "ANY");

        await using var firstLease = await manager.AcquireAsync(key, UsbCaptureSettings.Default, CancellationToken.None);
        await using var secondLease = await manager.AcquireAsync(key, UsbCaptureSettings.Default, CancellationToken.None);

        var firstFrame = await firstLease.WaitForNextFrameAsync(previousVersion: 0, TimeSpan.FromSeconds(1), CancellationToken.None);
        var secondFrame = await secondLease.WaitForNextFrameAsync(previousVersion: 0, TimeSpan.FromSeconds(1), CancellationToken.None);

        Assert.Equal(1, backend.GetOpenCount(key));
        Assert.NotNull(firstFrame);
        Assert.NotNull(secondFrame);
        Assert.Equal(firstFrame.Value.FrameVersion, secondFrame.Value.FrameVersion);
    }

    /// <summary>
    /// <description>Feature: UsbCameraOwnerManager starts independent physical owners for different camera sources.
    /// 
    ///   Scenario: Two distinct USB camera keys each get their own backend open and produce frames with correct source IDs.
    ///     Given a UsbCameraOwnerManager backed by FakeUsbCaptureBackend with two different keys (usb:0:ANY and usb:1:ANY),
    ///      And a first lease acquired for key 0,
    ///      And a second lease acquired for key 1,
    ///     When both leases wait for their next frame,
    ///     Then the backend open count for each key should be exactly 1,
    ///      And both frames should be non-null,
    ///      And the first frame SourceId should be "usb:0:ANY",
    ///      And the second frame SourceId should be "usb:1:ANY".</description>
    /// </summary>
    [Fact]
    public async Task AcquireAsync_DifferentCameraSources_StartsIndependentPhysicalOwners()
    {
        var backend = new FakeUsbCaptureBackend();
        await using var manager = new UsbCameraOwnerManager(backend);
        var firstKey = new UsbCameraKey(0, "ANY");
        var secondKey = new UsbCameraKey(1, "ANY");

        await using var firstLease = await manager.AcquireAsync(firstKey, UsbCaptureSettings.Default, CancellationToken.None);
        await using var secondLease = await manager.AcquireAsync(secondKey, UsbCaptureSettings.Default, CancellationToken.None);

        var firstFrame = await firstLease.WaitForNextFrameAsync(previousVersion: 0, TimeSpan.FromSeconds(1), CancellationToken.None);
        var secondFrame = await secondLease.WaitForNextFrameAsync(previousVersion: 0, TimeSpan.FromSeconds(1), CancellationToken.None);

        Assert.Equal(1, backend.GetOpenCount(firstKey));
        Assert.Equal(1, backend.GetOpenCount(secondKey));
        Assert.NotNull(firstFrame);
        Assert.NotNull(secondFrame);
        Assert.Equal("usb:0:ANY", firstFrame.Value.SourceId);
        Assert.Equal("usb:1:ANY", secondFrame.Value.SourceId);
    }

    /// <summary>
    /// <description>Feature: UsbCameraOwnerManager returns increasing frame versions on successive reads.
    /// 
    ///   Scenario: Each call to WaitForNextFrameAsync with an incremented previousVersion produces a newer frame.
    ///     Given a UsbCameraOwnerManager backed by FakeUsbCaptureBackend that produces 2 frames per session,
    ///      And a lease acquired for camera key (usb:0:ANY),
    ///     When the first call to WaitForNextFrameAsync(previousVersion: 0) returns a frame,
    ///      And the second call to WaitForNextFrameAsync(firstFrame.FrameVersion) is made,
    ///     Then both frames should be non-null,
    ///      And the second FrameVersion should be greater than the first,
    ///      And the second frame Width should be 2,
    ///      And the second frame Height should be 2,
    ///      And the second frame EncodedJpeg should not be empty.</description>
    /// </summary>
    [Fact]
    public async Task WaitForNextFrameAsync_ReturnsIncreasingFrameVersions()
    {
        var backend = new FakeUsbCaptureBackend(framesPerSession: 2, frameDelay: TimeSpan.FromMilliseconds(25));
        await using var manager = new UsbCameraOwnerManager(backend);
        var key = new UsbCameraKey(0, "ANY");

        await using var lease = await manager.AcquireAsync(key, UsbCaptureSettings.Default, CancellationToken.None);

        var firstFrame = await lease.WaitForNextFrameAsync(previousVersion: 0, TimeSpan.FromSeconds(1), CancellationToken.None);
        var secondFrame = await lease.WaitForNextFrameAsync(firstFrame?.FrameVersion ?? 0, TimeSpan.FromSeconds(1), CancellationToken.None);

        Assert.NotNull(firstFrame);
        Assert.NotNull(secondFrame);
        Assert.True(secondFrame.Value.FrameVersion > firstFrame.Value.FrameVersion);
        Assert.Equal(2, secondFrame.Value.Width);
        Assert.Equal(2, secondFrame.Value.Height);
        Assert.NotEmpty(secondFrame.Value.EncodedJpeg);
    }

    /// <summary>
    /// <description>Feature: UsbCameraOwnerManager returns no frame without throwing when the caller cancels.
    /// 
    ///   Scenario: A pre-cancelled CancellationToken causes WaitForNextFrameAsync to return null immediately instead of throwing.
    ///     Given a UsbCameraOwnerManager backed by FakeUsbCaptureBackend that produces 0 frames per session,
    ///      And a lease acquired for camera key (usb:0:ANY),
    ///     When a CancellationTokenSource is cancelled before calling WaitForNextFrameAsync,
    ///     Then the returned frame should be null.</description>
    /// </summary>
    [Fact]
    public async Task WaitForNextFrameAsync_WhenCallerCancels_ReturnsNoFrameWithoutThrowing()
    {
        var backend = new FakeUsbCaptureBackend(framesPerSession: 0);
        await using var manager = new UsbCameraOwnerManager(backend);
        var key = new UsbCameraKey(0, "ANY");
        await using var lease = await manager.AcquireAsync(key, UsbCaptureSettings.Default, CancellationToken.None);
        using var cts = new CancellationTokenSource();

        await cts.CancelAsync();
        var frame = await lease.WaitForNextFrameAsync(previousVersion: 0, TimeSpan.FromSeconds(1), cts.Token);

        Assert.Null(frame);
    }

    /// <summary>
    /// <description>Feature: UsbCameraOwnerManager reports correct runtime status after a frame is produced.
    /// 
    ///   Scenario: GetStatus reflects running state with latest frame metadata and no staleness.
    ///     Given a UsbCameraOwnerManager backed by FakeUsbCaptureBackend with one camera key,
    ///      And a lease acquired from the manager,
    ///     When the lease reads its next frame and GetStatus is called with the frame timestamp,
    ///     Then the status State should be Running,
    ///      And IsStale should be false,
    ///      And LatestFrameVersion should match the frame's FrameVersion,
    ///      And Width should match the frame's Width,
    ///      And Height should match the frame's Height.</description>
    /// </summary>
    [Fact]
    public async Task GetStatus_AfterCameraSourceProducesFrame_ReportsRunningWithLatestFrameMetadata()
    {
        var backend = new FakeUsbCaptureBackend();
        await using var manager = new UsbCameraOwnerManager(backend);
        var key = new UsbCameraKey(0, "ANY");

        await using var lease = await manager.AcquireAsync(key, UsbCaptureSettings.Default, CancellationToken.None);
        var frame = await lease.WaitForNextFrameAsync(previousVersion: 0, TimeSpan.FromSeconds(1), CancellationToken.None);

        var status = manager.GetStatus(key, nowUtcMs: frame?.TimestampUtcMs ?? 0);

        Assert.Equal(UsbCameraOwnerState.Running, status.State);
        Assert.False(status.IsStale);
        Assert.Equal(frame?.FrameVersion, status.LatestFrameVersion);
        Assert.Equal(frame?.Width, status.Width);
        Assert.Equal(frame?.Height, status.Height);
    }

    /// <summary>
    /// <description>Feature: UsbCameraOwnerManager reports stale frame age when the latest frame is older than one second.
    /// 
    ///   Scenario: GetStatus with a timestamp 1001ms after the last frame produces a stale status with correct FrameAgeMs.
    ///     Given a UsbCameraOwnerManager backed by FakeUsbCaptureBackend with one camera key,
    ///      And a lease acquired from the manager,
    ///     When the lease reads its next frame and GetStatus is called with timestamp equal to (frame.TimestampUtcMs + 1001),
    ///     Then the status State should be Running,
    ///      And IsStale should be true,
    ///      And FrameAgeMs should be exactly 1001.</description>
    /// </summary>
    [Fact]
    public async Task GetStatus_WhenLatestFrameIsOlderThanOneSecond_ReportsStaleFrameAge()
    {
        var backend = new FakeUsbCaptureBackend();
        await using var manager = new UsbCameraOwnerManager(backend);
        var key = new UsbCameraKey(0, "ANY");

        await using var lease = await manager.AcquireAsync(key, UsbCaptureSettings.Default, CancellationToken.None);
        var frame = await lease.WaitForNextFrameAsync(previousVersion: 0, TimeSpan.FromSeconds(1), CancellationToken.None);

        var status = manager.GetStatus(key, nowUtcMs: (frame?.TimestampUtcMs ?? 0) + 1001);

        Assert.Equal(UsbCameraOwnerState.Running, status.State);
        Assert.True(status.IsStale);
        Assert.Equal(1001, status.FrameAgeMs);
    }

    /// <summary>
    /// <description>Feature: UsbCameraOwnerManager reports failed state with failure message when open fails.
    /// 
    ///   Scenario: An open failure produces an InvalidOperationException and GetStatus reflects the failure state.
    ///     Given a UsbCameraOwnerManager backed by FakeUsbCaptureBackend configured to fail opens with message "camera unavailable",
    ///      And one camera key (usb:0:ANY),
    ///     When AcquireAsync is called,
    ///     Then an InvalidOperationException should be thrown,
    ///      And GetStatus should return State equal to Failed,
    ///      And FailureMessage should equal "camera unavailable".</description>
    /// </summary>
    [Fact]
    public async Task GetStatus_WhenOpenFails_ReportsFailedWithMessage()
    {
        var backend = new FakeUsbCaptureBackend(openFailure: "camera unavailable");
        await using var manager = new UsbCameraOwnerManager(backend);
        var key = new UsbCameraKey(0, "ANY");

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await manager.AcquireAsync(key, UsbCaptureSettings.Default, CancellationToken.None));

        var status = manager.GetStatus(key, nowUtcMs: 0);

        Assert.Equal(UsbCameraOwnerState.Failed, status.State);
        Assert.Equal("camera unavailable", status.FailureMessage);
    }

    /// <summary>
    /// <description>Feature: UsbCameraOwnerManager updates status to Running when RestartAsync succeeds after a failed open.
    /// 
    ///   Scenario: A ToggleableUsbCaptureBackend that transitions from failure to success allows the manager to recover on restart.
    ///     Given a UsbCameraOwnerManager backed by ToggleableUsbCaptureBackend configured to fail opens with message "camera unavailable",
    ///      And one camera key (usb:0:ANY),
    ///     When AcquireAsync is called and throws InvalidOperationException,
    ///      And backend.OpenFailure is set to null (simulating recovery),
    ///      And RestartAsync is called with the same key and settings,
    ///     Then GetStatus should return State equal to Running,
    ///      And FailureMessage should be null,
    ///      And backend.GetOpenCount(key) should be 2 (one failed attempt + one successful restart).</description>
    /// </summary>
    [Fact]
    public async Task RestartAsync_AfterFailedOpen_UpdatesStatusToRunningWhenCameraSourceRecovers()
    {
        var backend = new ToggleableUsbCaptureBackend(openFailure: "camera unavailable");
        await using var manager = new UsbCameraOwnerManager(backend);
        var key = new UsbCameraKey(0, "ANY");

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await manager.AcquireAsync(key, UsbCaptureSettings.Default, CancellationToken.None));

        backend.OpenFailure = null;
        await manager.RestartAsync(key, UsbCaptureSettings.Default, CancellationToken.None);
        var status = manager.GetStatus(key, nowUtcMs: 1);

        Assert.Equal(UsbCameraOwnerState.Running, status.State);
        Assert.Null(status.FailureMessage);
        Assert.Equal(2, backend.GetOpenCount(key));
    }

    /// <summary>
    /// <description>Feature: UsbCameraOwnerManager preserves previous failure message while a restart is in progress.
    /// 
    ///   Scenario: A ControlledRestartUsbCaptureBackend that blocks during open allows inspection of the Starting state with preserved failure context.
    ///     Given a UsbCameraOwnerManager backed by ControlledRestartUsbCaptureBackend configured to fail opens with message "camera unavailable",
    ///      And one camera key (usb:0:ANY),
    ///     When AcquireAsync is called and throws InvalidOperationException,
    ///      And backend.OpenFailure is set to null (simulating recovery),
    ///      And RestartAsync is called but blocked on WaitForOpenAttemptAsync(),
    ///     Then GetStatus should return State equal to Starting,
    ///      And FailureMessage should still be "camera unavailable" (preserved from previous failure).</description>
    /// </summary>
    [Fact]
    public async Task RestartAsync_WhileStarting_PreservesPreviousFailureMessage()
    {
        var backend = new ControlledRestartUsbCaptureBackend("camera unavailable");
        await using var manager = new UsbCameraOwnerManager(backend);
        var key = new UsbCameraKey(0, "ANY");

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await manager.AcquireAsync(key, UsbCaptureSettings.Default, CancellationToken.None));

        backend.OpenFailure = null;
        var restartTask = manager.RestartAsync(key, UsbCaptureSettings.Default, CancellationToken.None);
        await backend.WaitForOpenAttemptAsync();

        var startingStatus = manager.GetStatus(key, nowUtcMs: 0);

        Assert.Equal(UsbCameraOwnerState.Starting, startingStatus.State);
        Assert.Equal("camera unavailable", startingStatus.FailureMessage);

        backend.CompleteOpen();
        await restartTask;
    }

    /// <summary>
    /// <description>Feature: UsbCameraOwnerManager retries stable baseline mode when the requested mode opens but produces no frames.
    /// 
    ///   Scenario: A FallbackModeUsbCaptureBackend that only succeeds at 20 FPS (not the requested 60) causes the manager to fall back gracefully.
    ///     Given a UsbCameraOwnerManager backed by FallbackModeUsbCaptureBackend,
    ///      And one camera key (usb:1:ANY),
    ///     When AcquireAsync is called with UsbCaptureSettings(640, 480, 60),
    ///      And the lease waits for its next frame,
    ///     Then the returned frame should be non-null,
    ///      And backend.OpenedTargetFps should contain [60, 20] (requested mode attempted first, then fallback).</description>
    /// </summary>
    [Fact]
    public async Task AcquireAsync_WhenRequestedModeOpensButProducesNoFrames_RetriesStableBaselineMode()
    {
        var backend = new FallbackModeUsbCaptureBackend();
        await using var manager = new UsbCameraOwnerManager(backend);
        var key = new UsbCameraKey(1, "ANY");

        await using var lease = await manager.AcquireAsync(key, new UsbCaptureSettings(640, 480, 60), CancellationToken.None);
        var frame = await lease.WaitForNextFrameAsync(previousVersion: 0, TimeSpan.FromSeconds(1), CancellationToken.None);

        Assert.NotNull(frame);
        Assert.Equal(new[] { 60, 20 }, backend.OpenedTargetFps.ToArray());
    }

    /// <summary>
    /// <description>Feature: UsbCameraOwnerManager samples background using shared USB feed without opening a competing camera handle.
    /// 
    ///   Scenario: SampleBackgroundAsync acquires frames through the manager, then a runtime lease can still access the same physical device.
    ///     Given a UsbCameraOwnerManager backed by ControlledSequenceUsbCaptureBackend with one camera key (usb:0:ANY),
    ///      And 3 background frames enqueued for sampling,
    ///     When SampleBackgroundAsync is called with sampleCount=3 and processMaxWidth=80,
    ///      And a runtime lease is acquired for the same key,
    ///      And a foreground frame (white rectangle) is enqueued after the background samples,
    ///      And VisualObservationPipeline observes the foreground frame with the sampled background as reference,
    ///     Then backend.GetOpenCount(key) should be exactly 1 (no competing handle opened),
    ///      And the observation result should contain exactly one MovingObjectObservation.</description>
    /// </summary>
    [Fact]
    public async Task SampleBackgroundAsync_UsesSharedUsbFeedWithoutOpeningCompetingCameraHandle()
    {
        var key = new UsbCameraKey(0, "ANY");
        var backend = new ControlledSequenceUsbCaptureBackend(key);
        await using var manager = new UsbCameraOwnerManager(backend);
        backend.Enqueue(CreateEncodedImage(80, 50));
        backend.Enqueue(CreateEncodedImage(80, 50));
        backend.Enqueue(CreateEncodedImage(80, 50));

        var encodedBackground = await manager.SampleBackgroundAsync(key, UsbCaptureSettings.Default, sampleCount: 3, processMaxWidth: 80, CancellationToken.None);
        await using var runtimeLease = await manager.AcquireAsync(key, UsbCaptureSettings.Default, CancellationToken.None);
        backend.Enqueue(CreateEncodedImage(80, 50, new Cv.Rect(20, 12, 12, 10)));
        var foregroundFrame = await runtimeLease.WaitForNextFrameAsync(previousVersion: 0, TimeSpan.FromSeconds(1), CancellationToken.None);
        var observationPipeline = new VisualObservationPipeline();

        var result = await observationPipeline.ObserveAsync(
            new ObjectTracker.Core.Domain.FramePacket(
                "usb:0:ANY",
                foregroundFrame?.TimestampUtcMs ?? 0,
                foregroundFrame?.Width ?? 0,
                foregroundFrame?.Height ?? 0,
                foregroundFrame?.EncodedJpeg ?? []),
            ObjectTracker.Core.Domain.VisualObservationSettings.Default with
            {
                Threshold = 20,
                MotionArea = 40,
                MorphKernelSize = 1,
                ProcessMaxWidth = 80,
                EncodedBackground = encodedBackground
            },
            CancellationToken.None);

        Assert.Equal(1, backend.GetOpenCount(key));
        Assert.Single(result.MovingObjectObservations);
    }

    /// <summary>
    /// <description>Feature: UsbCameraOwnerManager disposes active physical owners when StopAllAsync is called.
    /// 
    ///   Scenario: Two leases for different camera keys are both disposed after calling StopAllAsync.
    ///     Given a UsbCameraOwnerManager backed by FakeUsbCaptureBackend with two camera keys (usb:0:ANY and usb:1:ANY),
    ///      And both leases acquired from the manager,
    ///     When StopAllAsync is called with CancellationToken.None,
    ///     Then backend.GetDisposeCount(firstKey) should be 1,
    ///      And backend.GetDisposeCount(secondKey) should be 1.</description>
    /// </summary>
    [Fact]
    public async Task StopAllAsync_DisposesActivePhysicalOwners()
    {
        var backend = new FakeUsbCaptureBackend();
        await using var manager = new UsbCameraOwnerManager(backend);
        var firstKey = new UsbCameraKey(0, "ANY");
        var secondKey = new UsbCameraKey(1, "ANY");

        await using var firstLease = await manager.AcquireAsync(firstKey, UsbCaptureSettings.Default, CancellationToken.None);
        await using var secondLease = await manager.AcquireAsync(secondKey, UsbCaptureSettings.Default, CancellationToken.None);

        await manager.StopAllAsync(CancellationToken.None);

        Assert.Equal(1, backend.GetDisposeCount(firstKey));
        Assert.Equal(1, backend.GetDisposeCount(secondKey));
    }

    /// <summary>
    /// <description>Feature: UsbCameraOwnerManager does not throw FirstChanceTaskCanceledException when stopping with an empty read loop waiting.
    /// 
    ///   Scenario: Calling StopAllAsync while a lease's read loop is blocked (waiting for frames that never arrive) should not produce TaskCanceledExceptions in the manager call stack.
    ///     Given a UsbCameraOwnerManager backed by FakeUsbCaptureBackend that produces 0 frames per session,
    ///      And one camera key (usb:0:ANY),
    ///      And a FirstChanceException handler counts TaskCanceledExceptions originating from UsbCameraOwnerManager,
    ///     When a lease is acquired and we wait 25ms (allowing the read loop to enter its idle state),
    ///      And StopAllAsync is called with CancellationToken.None,
    ///     Then taskCanceledExceptions should be exactly 0.</description>
    /// </summary>
    [Fact]
    public async Task StopAllAsync_WhenEmptyReadLoopIsWaiting_DoesNotThrowFirstChanceTaskCanceledException()
    {
        var backend = new FakeUsbCaptureBackend(framesPerSession: 0);
        await using var manager = new UsbCameraOwnerManager(backend);
        var key = new UsbCameraKey(0, "ANY");
        var taskCanceledExceptions = 0;
        void OnFirstChanceException(object? sender, FirstChanceExceptionEventArgs args)
        {
            if (args.Exception is TaskCanceledException && args.Exception.StackTrace?.Contains("UsbCameraOwnerManager", StringComparison.Ordinal) == true)
            {
                taskCanceledExceptions++;
            }
        }

        AppDomain.CurrentDomain.FirstChanceException += OnFirstChanceException;
        try
        {
            await using var lease = await manager.AcquireAsync(key, UsbCaptureSettings.Default, CancellationToken.None);
            await Task.Delay(25);

            await manager.StopAllAsync(CancellationToken.None);
        }
        finally
        {
            AppDomain.CurrentDomain.FirstChanceException -= OnFirstChanceException;
        }

        Assert.Equal(0, taskCanceledExceptions);
    }

    /// <summary>
    /// <description>Feature: UsbCameraOwnerManager serializes physical open when multiple camera sources start concurrently.
    /// 
    ///   Scenario: Two concurrent AcquireAsync calls for different keys are serialized so only one opens at a time.
    ///     Given a UsbCameraOwnerManager backed by FakeUsbCaptureBackend configured with a 25ms open delay,
    ///     When manager.AcquireAsync(key0) and manager.AcquireAsync(key1) are started simultaneously via Task.WhenAll,
    ///     Then backend.MaxConcurrentOpens should be exactly 1.</description>
    /// </summary>
    [Fact]
    public async Task AcquireAsync_ConcurrentCameraSourceStartup_SerializesPhysicalOpen()
    {
        var backend = new FakeUsbCaptureBackend(openDelay: TimeSpan.FromMilliseconds(25));
        await using var manager = new UsbCameraOwnerManager(backend);

        await Task.WhenAll(
            manager.AcquireAsync(new UsbCameraKey(0, "ANY"), UsbCaptureSettings.Default, CancellationToken.None).AsTask(),
            manager.AcquireAsync(new UsbCameraKey(1, "ANY"), UsbCaptureSettings.Default, CancellationToken.None).AsTask());

        Assert.Equal(1, backend.MaxConcurrentOpens);
    }

    private sealed class FakeUsbCaptureBackend(TimeSpan? openDelay = null, int framesPerSession = 1, TimeSpan? frameDelay = null, string? openFailure = null) : IUsbCaptureBackend
    {
        private readonly Dictionary<UsbCameraKey, int> openCounts = new();
        private readonly Dictionary<UsbCameraKey, int> disposeCounts = new();
        private int activeOpens;

        public int GetOpenCount(UsbCameraKey key) => openCounts.TryGetValue(key, out var count) ? count : 0;

        public int GetDisposeCount(UsbCameraKey key) => disposeCounts.TryGetValue(key, out var count) ? count : 0;

        public int MaxConcurrentOpens { get; private set; }

        public async ValueTask<IUsbCaptureSession> OpenAsync(UsbCameraKey key, UsbCaptureSettings settings, CancellationToken cancellationToken)
        {
            activeOpens++;
            MaxConcurrentOpens = Math.Max(MaxConcurrentOpens, activeOpens);
            if (openDelay is { } delay)
            {
                await Task.Delay(delay, cancellationToken);
            }

            if (openFailure is not null)
            {
                activeOpens--;
                throw new InvalidOperationException(openFailure);
            }

            openCounts[key] = GetOpenCount(key) + 1;
            activeOpens--;
            return new FakeUsbCaptureSession(key, framesPerSession, frameDelay, () => disposeCounts[key] = GetDisposeCount(key) + 1);
        }
    }

    private sealed class FakeUsbCaptureSession(UsbCameraKey key, int framesPerSession, TimeSpan? frameDelay, Action onDispose) : IUsbCaptureSession
    {
        private long timestampUtcMs = 1;
        private int returnedFrames;

        public async ValueTask<UsbCapturedFrame?> ReadFrameAsync(CancellationToken cancellationToken)
        {
            if (returnedFrames >= framesPerSession)
            {
                return null;
            }

            if (returnedFrames > 0 && frameDelay is { } delay)
            {
                await Task.Delay(delay, cancellationToken);
            }

            returnedFrames++;
            return new UsbCapturedFrame(
                UsbCameraSourceId.Format(key),
                timestampUtcMs++,
                Width: 2,
                Height: 2,
                EncodedJpeg: [1, 2, 3]);
        }

        public ValueTask DisposeAsync()
        {
            onDispose();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ToggleableUsbCaptureBackend(string? openFailure) : IUsbCaptureBackend
    {
        private readonly Dictionary<UsbCameraKey, int> openCounts = new();

        public string? OpenFailure { get; set; } = openFailure;

        public int GetOpenCount(UsbCameraKey key) => openCounts.TryGetValue(key, out var count) ? count : 0;

        public ValueTask<IUsbCaptureSession> OpenAsync(UsbCameraKey key, UsbCaptureSettings settings, CancellationToken cancellationToken)
        {
            openCounts[key] = GetOpenCount(key) + 1;
            if (OpenFailure is not null)
            {
                throw new InvalidOperationException(OpenFailure);
            }

            return ValueTask.FromResult<IUsbCaptureSession>(new FakeUsbCaptureSession(key, framesPerSession: 1, frameDelay: null, onDispose: () => { }));
        }
    }

    private sealed class ControlledRestartUsbCaptureBackend(string? openFailure) : IUsbCaptureBackend
    {
        private readonly TaskCompletionSource<object?> openAttempted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<object?> completeOpen = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string? OpenFailure { get; set; } = openFailure;

        public async ValueTask<IUsbCaptureSession> OpenAsync(UsbCameraKey key, UsbCaptureSettings settings, CancellationToken cancellationToken)
        {
            if (OpenFailure is not null)
            {
                throw new InvalidOperationException(OpenFailure);
            }

            openAttempted.TrySetResult(null);
            await completeOpen.Task.WaitAsync(cancellationToken);
            return new FakeUsbCaptureSession(key, framesPerSession: 1, frameDelay: null, onDispose: () => { });
        }

        public Task WaitForOpenAttemptAsync() => openAttempted.Task;

        public void CompleteOpen() => completeOpen.TrySetResult(null);
    }

    private sealed class FallbackModeUsbCaptureBackend : IUsbCaptureBackend
    {
        public List<int> OpenedTargetFps { get; } = new();

        public ValueTask<IUsbCaptureSession> OpenAsync(UsbCameraKey key, UsbCaptureSettings settings, CancellationToken cancellationToken)
        {
            OpenedTargetFps.Add(settings.TargetFps);
            var frames = settings.TargetFps == 20 ? 1 : 0;
            return ValueTask.FromResult<IUsbCaptureSession>(new FakeUsbCaptureSession(key, frames, frameDelay: null, onDispose: () => { }));
        }
    }

    private sealed class ControlledSequenceUsbCaptureBackend(UsbCameraKey expectedKey) : IUsbCaptureBackend
    {
        private readonly Dictionary<UsbCameraKey, int> openCounts = new();
        private readonly Queue<byte[]> frames = new();
        private readonly SemaphoreSlim frameAvailable = new(0);

        public int GetOpenCount(UsbCameraKey key) => openCounts.TryGetValue(key, out var count) ? count : 0;

        public void Enqueue(byte[] encodedFrame)
        {
            lock (frames)
            {
                frames.Enqueue(encodedFrame);
            }

            frameAvailable.Release();
        }

        public ValueTask<IUsbCaptureSession> OpenAsync(UsbCameraKey key, UsbCaptureSettings settings, CancellationToken cancellationToken)
        {
            Assert.Equal(expectedKey, key);
            openCounts[key] = GetOpenCount(key) + 1;
            return ValueTask.FromResult<IUsbCaptureSession>(new ControlledSequenceUsbCaptureSession(key, frames, frameAvailable));
        }
    }

    private sealed class ControlledSequenceUsbCaptureSession(UsbCameraKey key, Queue<byte[]> frames, SemaphoreSlim frameAvailable) : IUsbCaptureSession
    {
        private long timestampUtcMs;

        public async ValueTask<UsbCapturedFrame?> ReadFrameAsync(CancellationToken cancellationToken)
        {
            try
            {
                await frameAvailable.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return null;
            }

            byte[] encodedFrame;
            lock (frames)
            {
                encodedFrame = frames.Dequeue();
            }

            return new UsbCapturedFrame(
                UsbCameraSourceId.Format(key),
                TimestampUtcMs: ++timestampUtcMs,
                Width: 80,
                Height: 50,
                EncodedJpeg: encodedFrame);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private static byte[] CreateEncodedImage(int width, int height, Cv.Rect? foreground = null)
    {
        using var image = new Cv.Mat(height, width, Cv.MatType.CV_8UC3, Cv.Scalar.Black);
        if (foreground is { } rect)
        {
            Cv.Cv2.Rectangle(image, rect, Cv.Scalar.White, -1);
        }

        Cv.Cv2.ImEncode(".jpg", image, out var encoded, [new Cv.ImageEncodingParam(Cv.ImwriteFlags.JpegQuality, 100)]);
        return encoded;
    }
}
