using ObjectTracker.UI.Desktop.Region.Contracts;

namespace ObjectTracker.UI.Desktop.Region.Contracts;

public interface IRegionProcessorService
{
    EnrichedTrainState? ProcessDetection(TrainDetection detection, string zoneId);
}
