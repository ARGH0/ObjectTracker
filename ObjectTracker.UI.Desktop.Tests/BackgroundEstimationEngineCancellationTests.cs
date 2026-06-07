using System.Diagnostics;
using System.Reflection;
using Xunit;

namespace ObjectTracker.UI.Desktop.Tests;

public sealed class BackgroundEstimationEngineCancellationTests
{
    private static MethodInfo GetWaitForPlaybackScheduleMethod()
    {
        var assembly = Assembly.Load("ObjectTracker.UI.Desktop");
        var engineType = assembly.GetType("ObjectTracker.UI.Desktop.BackgroundEstimationEngine");
        Assert.NotNull(engineType);

        var method = engineType!.GetMethod(
            "WaitForPlaybackScheduleAsync",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        return method!;
    }

    /// <summary>
    /// <description>Feature: BackgroundEstimationEngine.WaitForPlaybackScheduleAsync handles cancellation gracefully.
    /// 
    ///   Scenario: Playback schedule wait is cancelled before it completes, no exception should be thrown.
    ///     Given a CancellationTokenSource that has not been cancelled,
    ///      And WaitForPlaybackScheduleAsync is invoked with the token and a 1 ms delay parameter,
    ///     When cts.Cancel() is called to cancel the token,
    ///      And the returned task completes,
    ///     Then no exception should be thrown.</description>
    /// </summary>
    [Fact]
    public async Task WaitForPlaybackScheduleAsync_WhenTokenCancelled_DoesNotThrow()
    {
        var method = GetWaitForPlaybackScheduleMethod();

        using var cts = new CancellationTokenSource();
        var playbackClock = Stopwatch.StartNew();
        var task = (Task?)method!.Invoke(null, new object?[]
        {
            1,
            30d,
            playbackClock,
            null,
            cts.Token
        });

        Assert.NotNull(task);

        cts.Cancel();

        var exception = await Record.ExceptionAsync(async () => await task!);
        Assert.Null(exception);
    }

    /// <summary>
    /// <description>Feature: BackgroundEstimationEngine.WaitForPlaybackScheduleAsync responds quickly to cancellation.
    /// 
    ///   Scenario: Playback schedule wait is cancelled and completes within a short time window.
    ///     Given a CancellationTokenSource that has not been cancelled,
    ///      And WaitForPlaybackScheduleAsync is invoked with a large delay parameter (10 000 ms),
    ///     When the task starts running,
    ///      And cts.Cancel() is called after 5 ms of waiting,
    ///     Then the task should complete within 250 ms of cancellation.</description>
    /// </summary>
    [Fact]
    public async Task WaitForPlaybackScheduleAsync_WhenTokenCancelled_StopsQuickly()
    {
        var method = GetWaitForPlaybackScheduleMethod();

        using var cts = new CancellationTokenSource();
        var playbackClock = Stopwatch.StartNew();
        var task = (Task?)method.Invoke(null, new object?[]
        {
            10_000,
            30d,
            playbackClock,
            null,
            cts.Token
        });

        Assert.NotNull(task);

        await Task.Delay(5);
        var cancelToCompletion = Stopwatch.StartNew();
        cts.Cancel();

        await task!;

        Assert.True(cancelToCompletion.Elapsed < TimeSpan.FromMilliseconds(250));
    }
}
