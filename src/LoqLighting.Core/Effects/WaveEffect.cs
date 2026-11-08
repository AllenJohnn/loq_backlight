namespace LoqLighting.Core.Effects;

public class WaveEffect : IEffect
{
    public string Name => "Wave";

    public IEnumerable<Step> Generate(EffectSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var offHold = settings.ScaleHold(TimeSpan.FromMilliseconds(100));
        var lowHold = settings.ScaleHold(TimeSpan.FromMilliseconds(150));
        var crestHold = settings.ScaleHold(TimeSpan.FromMilliseconds(300));

        while (true)
        {
            // Swell up
            yield return new Step(Brightness.Off, offHold);
            yield return new Step(Brightness.Low, lowHold);
            yield return new Step(Brightness.High, crestHold);

            // Swell down
            yield return new Step(Brightness.Low, lowHold);
        }
    }
}
