namespace ObjectTracker.Core.Ports;

public interface IColorFilterControl
{
    IReadOnlyList<string> AvailableColors { get; }
    IReadOnlyList<string> EnabledColors { get; }
    void SetEnabledColors(IEnumerable<string> colors);
}