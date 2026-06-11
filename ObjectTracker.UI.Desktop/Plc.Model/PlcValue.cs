using System;
using ObjectTracker.UI.Desktop.Plc.Model;

public readonly record struct PlcValue(PlcVariableType Type, object? Raw)
{
    public static PlcValue Bool(bool value) => new(PlcVariableType.Bool, value);
    public static PlcValue Int16(short value) => new(PlcVariableType.Int16, value);
    public static PlcValue Int32(int value) => new(PlcVariableType.Int32, value);
    public static PlcValue Real(float value) => new(PlcVariableType.Real, value);

    public bool AsBool() => Type == PlcVariableType.Bool && Raw is bool b ? b : throw Invalid("bool");
    public short AsInt16() => Type == PlcVariableType.Int16 && Raw is short i ? i : throw Invalid("Int16");
    public int AsInt32() => Type == PlcVariableType.Int32 && Raw is int i ? i : throw Invalid("Int32");
    public float AsReal() => Type == PlcVariableType.Real && Raw is float r ? r : throw Invalid("Real");

    private Exception Invalid(string expected) =>
        new InvalidCastException($"Cannot convert PlcValue[{Type}] to {expected}");
}
