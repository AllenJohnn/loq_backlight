using LoqLighting.Core.Effects;
using Xunit;

namespace LoqLighting.Core.Tests;

public class StrobeEffectTests
{
    [Fact]
    public void GeneratesAlternatingHighAndLowSteps()
    {
        var effect = new StrobeEffect();
        var settings = new EffectSettings
        {
            LowLevel = Brightness.Low,
            HighLevel = Brightness.High,
            Speed = 1.0
        };

        var steps = effect.Generate(settings).Take(4).ToList();

        Assert.Equal(4, steps.Count);
        Assert.Equal(Brightness.High, steps[0].Level);
        Assert.Equal(Brightness.Low, steps[1].Level);
        Assert.Equal(Brightness.High, steps[2].Level);
        Assert.Equal(Brightness.Low, steps[3].Level);
        Assert.Equal(TimeSpan.FromMilliseconds(100), steps[0].Hold);
    }
}
