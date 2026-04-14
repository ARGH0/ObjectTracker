using System.Globalization;

namespace ObjectTracker.UI.Desktop;

/// <summary>
/// Describes an adjustable numeric sample setting that can be surfaced in the UI.
/// </summary>
internal sealed class SampleSetting
{
    private readonly object _sync = new();
    private double _value;

    private SampleSetting(
        string key,
        string label,
        string? hint,
        double defaultValue,
        double minimum,
        double maximum,
        double step,
        int decimals)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("A setting key is required.", nameof(key));
        }

        if (string.IsNullOrWhiteSpace(label))
        {
            throw new ArgumentException("A setting label is required.", nameof(label));
        }

        Key = key;
        Label = label;
        Hint = hint;
        Minimum = minimum;
        Maximum = maximum;
        Step = step;
        Decimals = Math.Max(0, decimals);
        _value = Normalize(defaultValue);
    }

    public string Key { get; }

    public string Label { get; }

    public string? Hint { get; }

    public double Minimum { get; }

    public double Maximum { get; }

    public double Step { get; }

    public int Decimals { get; }

    public double Value
    {
        get
        {
            lock (_sync)
            {
                return _value;
            }
        }
        set
        {
            lock (_sync)
            {
                _value = Normalize(value);
            }
        }
    }

    public int IntValue => (int)Math.Round(Value);

    public string FormatValue()
    {
        return Value.ToString($"F{Decimals}", CultureInfo.InvariantCulture);
    }

    public static SampleSetting Integer(
        string key,
        string label,
        int defaultValue,
        int minimum,
        int maximum,
        int step = 1,
        string? hint = null)
    {
        return new SampleSetting(key, label, hint, defaultValue, minimum, maximum, Math.Max(1, step), 0);
    }

    public static SampleSetting Decimal(
        string key,
        string label,
        double defaultValue,
        double minimum,
        double maximum,
        double step,
        int decimals,
        string? hint = null)
    {
        return new SampleSetting(key, label, hint, defaultValue, minimum, maximum, step, decimals);
    }

    private double Normalize(double value)
    {
        var clamped = Math.Clamp(value, Minimum, Maximum);
        if (Step > 0)
        {
            var steps = Math.Round((clamped - Minimum) / Step);
            clamped = Minimum + (steps * Step);
        }

        return Math.Round(clamped, Decimals, MidpointRounding.AwayFromZero);
    }
}