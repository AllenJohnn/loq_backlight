namespace LoqLighting.Core;

public sealed record EffectSettings
{
    private readonly double _speed = 1.0;

    public double Speed
    {
        get => _speed;
        init => _speed = Math.Clamp(value, 0.25, 4.0);
    }

    public Brightness LowLevel { get; init; } = Brightness.Low;
    public Brightness HighLevel { get; init; } = Brightness.High;
    public TimeSpan PauseDuration { get; init; } = TimeSpan.FromMilliseconds(500);
    public TimeSpan? RunDuration { get; init; } = null;

    public TimeSpan ScaleHold(TimeSpan baseHold)
    {
        double speed = Math.Clamp(Speed, 0.25, 4.0);
        double ms = baseHold.TotalMilliseconds / speed;
        return TimeSpan.FromMilliseconds(ms);
    }
}
