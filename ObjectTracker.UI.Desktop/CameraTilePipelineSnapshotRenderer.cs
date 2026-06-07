using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ObjectTracker.Core.Domain;
using ObjectTracker.UI.Desktop.Enums;

namespace ObjectTracker.UI.Desktop;

public readonly record struct CameraTileDebugFrameSnapshot(
    string CameraId,
    string Name,
    CameraTileDebugFrameSlot Slot,
    long FrameVersion,
    int Width,
    int Height,
    byte[] EncodedJpeg);

public sealed class CameraTilePipelineSnapshotRenderer
{
    public Task RenderSnapshotAsync(
        PipelineSnapshot snapshot,
        MainWindow.CameraTileFrameRouting routing,
        Func<CameraTileRawFrameSnapshot, Task> onFrame,
        CancellationToken cancellationToken)
    {
        return RenderSnapshotAsync(snapshot, routing, onFrame, _ => Task.CompletedTask, cancellationToken);
    }

    public async Task RenderSnapshotAsync(
        PipelineSnapshot snapshot,
        MainWindow.CameraTileFrameRouting routing,
        Func<CameraTileRawFrameSnapshot, Task> onFrame,
        Func<CameraTileDebugFrameSnapshot, Task> onDebugFrame,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var route = routing.Routes.FirstOrDefault(route => string.Equals(route.CameraId, snapshot.CameraSourceId, StringComparison.OrdinalIgnoreCase));
        if (route.FrameSource == CameraTileFrameSource.PipelineSnapshotAnnotatedFrame)
        {
            var frame = snapshot.AnnotatedFrame;
            await onFrame(new CameraTileRawFrameSnapshot(
                snapshot.CameraSourceId,
                frame.TimestampUtcMs,
                frame.Width,
                frame.Height,
                frame.EncodedJpeg));
            return;
        }

        if (route.FrameSource != CameraTileFrameSource.PipelineSnapshotDebugFrames)
        {
            return;
        }

        foreach (var debugFrame in snapshot.DebugFrames)
        {
            var frame = debugFrame.Frame;
            await onDebugFrame(new CameraTileDebugFrameSnapshot(
                snapshot.CameraSourceId,
                debugFrame.Name,
                GetDebugFrameSlot(debugFrame.Name),
                frame.TimestampUtcMs,
                frame.Width,
                frame.Height,
                frame.EncodedJpeg));
        }
    }

    private static CameraTileDebugFrameSlot GetDebugFrameSlot(string name)
    {
        return name switch
        {
            "moving-object-observation" or "moving-color" or "moving-object-evidence" => CameraTileDebugFrameSlot.MovingObjectObservation,
            "train-observation" or "color-detection" or "train-color-evidence" => CameraTileDebugFrameSlot.TrainObservation,
            "train-tracking" or "motion" or "motion-mask" or "rail-roi" => CameraTileDebugFrameSlot.TrainTracking,
            _ => CameraTileDebugFrameSlot.Source
        };
    }
}
