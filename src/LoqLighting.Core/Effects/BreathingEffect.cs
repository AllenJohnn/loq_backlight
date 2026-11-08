namespace LoqLighting.Core.Effects;

public class BreathingEffect : IEffect
{
    public string Name => "Breathing";

    public IEnumerable<Step> Generate(EffectSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var stepHold = settings.ScaleHold(TimeSpan.FromMilliseconds(400));

        while (true)
        {
            yield return new Step(Brightness.Off, stepHold);
            yield return new Step(Brightness.Low, stepHold);
            yield return new Step(Brightness.High, stepHold);
            yield return new Step(Brightness.Low, stepHold);
        }
    }
}
