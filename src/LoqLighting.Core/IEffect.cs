namespace LoqLighting.Core;

public interface IEffect
{
    string Name { get; }
    IEnumerable<Step> Generate(EffectSettings settings);
}
