namespace LoqLighting.Core.Effects;

public class StrobeWithPauseEffect : IEffect
{
    public string Name => "StrobeWithPause";
    public int PulsesPerCycle { get; init; } = 3;

    public IEnumerable<Step> Generate(EffectSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var pulseHold = settings.ScaleHold(TimeSpan.FromMilliseconds(100));
        var pauseHold = settings.ScaleHold(settings.PauseDuration);

        while (true)
        {
            for (int i = 0; i < PulsesPerCycle; i++)
            {
                yield return new Step(settings.HighLevel, pulseHold);
                yield return new Step(settings.LowLevel, pulseHold);
            }

            yield return new Step(settings.LowLevel, pauseHold);
        }
    }
}
