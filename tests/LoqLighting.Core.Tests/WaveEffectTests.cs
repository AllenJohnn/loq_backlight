using LoqLighting.Core.Effects;
using Xunit;

namespace LoqLighting.Core.Tests;

public class WaveEffectTests
{
    [Fact]
    public void GeneratesRhythmicWaveSequence()
    {
        var effect = new WaveEffect();
        var settings = new EffectSettings { Speed = 1.0 };

        var steps = effect.Generate(settings).Take(4).ToList();

        Assert.Equal(4, steps.Count);
        Assert.Equal(Brightness.Off, steps[0].Level);
        Assert.Equal(Brightness.Low, steps[1].Level);
        Assert.Equal(Brightness.High, steps[2].Level);
        Assert.Equal(Brightness.Low, steps[3].Level);
        Assert.Equal(TimeSpan.FromMilliseconds(300), steps[2].Hold);
    }
}
