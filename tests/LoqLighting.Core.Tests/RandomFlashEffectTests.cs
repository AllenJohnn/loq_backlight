using LoqLighting.Core.Effects;
using Xunit;

namespace LoqLighting.Core.Tests;

public class RandomFlashEffectTests
{
    [Fact]
    public void GeneratesRandomFlashStepsWithinRange()
    {
        var effect = new RandomFlashEffect(seed: 42);
        var settings = new EffectSettings { Speed = 1.0 };

        var steps = effect.Generate(settings).Take(10).ToList();

        Assert.Equal(10, steps.Count);
        foreach (var step in steps)
        {
            Assert.Contains(step.Level, new[] { Brightness.Off, Brightness.Low, Brightness.High });
            Assert.True(step.Hold >= TimeSpan.FromMilliseconds(50));
            Assert.True(step.Hold <= TimeSpan.FromMilliseconds(250));
        }
    }
}
