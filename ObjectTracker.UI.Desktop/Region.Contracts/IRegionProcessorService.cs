using System.Collections.Generic;

namespace ObjectTracker.UI.Desktop.Region.Contracts;

public interface IRegionProcessorService
{
    void ProcessDetection(TrainDetection detection);
    IEnumerable<EnrichedTrainState> GetLastProcessedStates();
}
