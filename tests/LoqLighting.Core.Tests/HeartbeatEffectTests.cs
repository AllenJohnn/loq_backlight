using LoqLighting.Core.Effects;
using Xunit;

namespace LoqLighting.Core.Tests;

public class HeartbeatEffectTests
{
    [Fact]
    public void GeneratesDoublePulseFollowedByRest()
    {
        var effect = new HeartbeatEffect();
        var settings = new EffectSettings
        {
            LowLevel = Brightness.Low,
            HighLevel = Brightness.High,
            PauseDuration = TimeSpan.FromMilliseconds(600),
            Speed = 1.0
        };

        var steps = effect.Generate(settings).Take(5).ToList();

        Assert.Equal(5, steps.Count);
        // Pulse 1
        Assert.Equal(Brightness.High, steps[0].Level);
        Assert.Equal(Brightness.Low, steps[1].Level);
        // Pulse 2
        Assert.Equal(Brightness.High, steps[2].Level);
        Assert.Equal(Brightness.Low, steps[3].Level);
        // Rest
        Assert.Equal(Brightness.Off, steps[4].Level);
        Assert.Equal(TimeSpan.FromMilliseconds(600), steps[4].Hold);
    }
}
