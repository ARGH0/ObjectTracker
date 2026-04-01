using ObjectTracker.Core.Ports;

namespace ObjectTracker.Vision;

public sealed class SystemClock : IClock
{
    public long UtcNowMs() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}