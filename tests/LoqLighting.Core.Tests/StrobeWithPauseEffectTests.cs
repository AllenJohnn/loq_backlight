using LoqLighting.Core.Effects;
using Xunit;

namespace LoqLighting.Core.Tests;

public class StrobeWithPauseEffectTests
{
    [Fact]
    public void GeneratesPulsesFollowedByPause()
    {
        var effect = new StrobeWithPauseEffect { PulsesPerCycle = 2 };
        var settings = new EffectSettings
        {
            LowLevel = Brightness.Low,
            HighLevel = Brightness.High,
            PauseDuration = TimeSpan.FromMilliseconds(500),
            Speed = 1.0
        };

        var steps = effect.Generate(settings).Take(5).ToList();

        // 2 pulses = 4 steps (High, Low, High, Low) + 1 pause step (Low for 500ms)
        Assert.Equal(5, steps.Count);
        Assert.Equal(Brightness.High, steps[0].Level);
        Assert.Equal(Brightness.Low, steps[1].Level);
        Assert.Equal(Brightness.High, steps[2].Level);
        Assert.Equal(Brightness.Low, steps[3].Level);
        Assert.Equal(Brightness.Low, steps[4].Level);
        Assert.Equal(TimeSpan.FromMilliseconds(500), steps[4].Hold);
    }
}
