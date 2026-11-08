namespace LoqLighting.Core.Effects;

public class RandomFlashEffect : IEffect
{
    public string Name => "RandomFlash";
    private readonly Random _random;

    public RandomFlashEffect(int? seed = null)
    {
        _random = seed.HasValue ? new Random(seed.Value) : Random.Shared;
    }

    public IEnumerable<Step> Generate(EffectSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        Brightness[] levels = { Brightness.Off, settings.LowLevel, settings.HighLevel };

        while (true)
        {
            var level = levels[_random.Next(levels.Length)];
            int holdMs = _random.Next(50, 251);
            var hold = settings.ScaleHold(TimeSpan.FromMilliseconds(holdMs));

            yield return new Step(level, hold);
        }
    }
}
