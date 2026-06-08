using System;
using ObjectTracker.Core.Ports;

namespace ObjectTracker;

public sealed class SystemClock : IClock
{
    public long UtcNowMs() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}
