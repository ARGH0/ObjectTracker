using System;
using ObjectTracker.UI.Desktop.Plc.Implementation;

namespace ObjectTracker.UI.Desktop.Plc.Siemens;

internal static class SiemensPlcExceptionMapper
{
    public static PlcException Map(Exception ex)
    {
        return ex is PlcException plcException
            ? plcException
            : new PlcException(0, ex.Message);
    }
}
