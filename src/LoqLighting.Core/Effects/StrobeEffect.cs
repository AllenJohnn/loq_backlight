namespace LoqLighting.Core.Effects;

public class StrobeEffect : IEffect
{
    public string Name => "Strobe";

    public IEnumerable<Step> Generate(EffectSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var hold = settings.ScaleHold(TimeSpan.FromMilliseconds(100));
        while (true)
        {
            yield return new Step(settings.HighLevel, hold);
            yield return new Step(settings.LowLevel, hold);
        }
    }
}
