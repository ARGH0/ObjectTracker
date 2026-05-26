using System;
using System.Diagnostics;

namespace ObjectTracker.Vision;

/// <summary>
/// Frame processing telemetry for performance monitoring and optimization.
/// Tracks time spent in each phase of the pipeline to identify bottlenecks.
/// </summary>
public sealed class FrameProcessingTelemetry
{
    private readonly Stopwatch _overallTimer;
    private readonly Stopwatch _phaseTimer;
    private long _decodeTimeMs;
    private long _detectionTimeMs;
    private long _trackingTimeMs;
    private long _renderingTimeMs;

    public long DecodeTimeMs => _decodeTimeMs;
    public long DetectionTimeMs => _detectionTimeMs;
    public long TrackingTimeMs => _trackingTimeMs;
    public long RenderingTimeMs => _renderingTimeMs;
    public long OverallTimeMs => _overallTimer.ElapsedMilliseconds;

    public FrameProcessingTelemetry()
    {
        _overallTimer = Stopwatch.StartNew();
        _phaseTimer = new Stopwatch();
    }

    public void StartPhase(ProcessingPhase phase)
    {
        _phaseTimer.Restart();
    }

    public void EndPhase(ProcessingPhase phase)
    {
        _phaseTimer.Stop();
        var elapsedMs = _phaseTimer.ElapsedMilliseconds;

        switch (phase)
        {
            case ProcessingPhase.Decode:
                _decodeTimeMs = elapsedMs;
                break;
            case ProcessingPhase.Detection:
                _detectionTimeMs = elapsedMs;
                break;
            case ProcessingPhase.Tracking:
                _trackingTimeMs = elapsedMs;
                break;
            case ProcessingPhase.Rendering:
                _renderingTimeMs = elapsedMs;
                break;
        }
    }

    public void ResetOverall()
    {
        _overallTimer.Restart();
    }

    public override string ToString() =>
        $"Decode:{DecodeTimeMs}ms  Detect:{DetectionTimeMs}ms  Track:{TrackingTimeMs}ms  Render:{RenderingTimeMs}ms  Total:{OverallTimeMs}ms";
}

public enum ProcessingPhase
{
    Decode,
    Detection,
    Tracking,
    Rendering
}
