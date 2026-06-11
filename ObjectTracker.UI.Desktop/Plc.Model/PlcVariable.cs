namespace ObjectTracker.UI.Desktop.Plc.Model;

public readonly record struct PlcVariable(string Address, PlcVariableType Type);
