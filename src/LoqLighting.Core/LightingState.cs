namespace LoqLighting.Core;

public readonly record struct LightingState(Brightness Level, byte StateType, byte RawLevel = 0)
{
    public bool IsKnownLevel => RawLevel is 1 or 2 or 3;
}
