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
