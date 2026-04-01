namespace ObjectTracker.Core.Ports;

public interface IClock
{
    long UtcNowMs();
}