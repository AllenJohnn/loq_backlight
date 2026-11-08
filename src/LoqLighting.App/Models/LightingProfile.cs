using LoqLighting.Core;

namespace LoqLighting.App.Models;

public sealed class LightingProfile
{
    public string Name { get; set; } = "Default";
    public string EffectName { get; set; } = "Strobe";
    public double Speed { get; set; } = 1.0;
    public bool IsInfiniteDuration { get; set; } = true;
    public int RunDurationSeconds { get; set; } = 10;
    public int PauseDurationMs { get; set; } = 500;
    public Brightness HighLevel { get; set; } = Brightness.High;
    public Brightness LowLevel { get; set; } = Brightness.Low;
}
