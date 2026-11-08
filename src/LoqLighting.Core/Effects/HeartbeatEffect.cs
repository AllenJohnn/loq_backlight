namespace LoqLighting.Core.Effects;

public class HeartbeatEffect : IEffect
{
    public string Name => "Heartbeat";

    public IEnumerable<Step> Generate(EffectSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var pulseHold = settings.ScaleHold(TimeSpan.FromMilliseconds(100));
        var gapHold = settings.ScaleHold(TimeSpan.FromMilliseconds(120));
        var restHold = settings.ScaleHold(settings.PauseDuration);

        while (true)
        {
            // Beat 1
            yield return new Step(settings.HighLevel, pulseHold);
            yield return new Step(settings.LowLevel, gapHold);

            // Beat 2
            yield return new Step(settings.HighLevel, pulseHold);
            yield return new Step(settings.LowLevel, gapHold);

            // Rest
            yield return new Step(Brightness.Off, restHold);
        }
    }
}
