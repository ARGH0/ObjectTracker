using System.Runtime.InteropServices;

namespace ObjectTracker.Core.Domain;

[StructLayout(LayoutKind.Auto)]
public readonly record struct RgbColor(byte R, byte G, byte B);
