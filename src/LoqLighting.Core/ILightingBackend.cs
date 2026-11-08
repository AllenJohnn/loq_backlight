namespace LoqLighting.Core;

public interface ILightingBackend : IAsyncDisposable
{
    Task<LightingState> ReadAsync(CancellationToken ct = default);
    Task WriteAsync(LightingState state, CancellationToken ct = default);
    Task RestoreAsync(LightingState state, CancellationToken ct = default);
    IReadOnlySet<Brightness> SupportedLevels { get; }
}
