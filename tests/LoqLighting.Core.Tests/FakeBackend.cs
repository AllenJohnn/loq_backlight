using LoqLighting.Core;

namespace LoqLighting.Core.Tests;

public class FakeBackend : ILightingBackend
{
    private static readonly HashSet<Brightness> AllLevels = new()
    {
        Brightness.Off,
        Brightness.Low,
        Brightness.High
    };

    public IReadOnlySet<Brightness> SupportedLevels => AllLevels;

    public LightingState CurrentState { get; set; } = new(Brightness.High, 0, 3);
    public List<LightingState> WrittenStates { get; } = new();
    public List<LightingState> RestoredStates { get; } = new();

    public int WriteCallCount { get; private set; }
    public int RestoreCallCount { get; private set; }

    public bool ThrowOnWrite { get; set; }
    public Exception? WriteException { get; set; }

    public bool ThrowOnRestore { get; set; }
    public Exception? RestoreException { get; set; }

    public Task<LightingState> ReadAsync(CancellationToken ct = default)
    {
        return Task.FromResult(CurrentState);
    }

    public Task WriteAsync(LightingState state, CancellationToken ct = default)
    {
        WriteCallCount++;
        if (ThrowOnWrite)
        {
            throw WriteException ?? new InvalidOperationException("FakeBackend write failure");
        }

        WrittenStates.Add(state);
        CurrentState = state;
        return Task.CompletedTask;
    }

    public Task RestoreAsync(LightingState state, CancellationToken ct = default)
    {
        RestoreCallCount++;
        if (ThrowOnRestore)
        {
            throw RestoreException ?? new InvalidOperationException("FakeBackend restore failure");
        }

        RestoredStates.Add(state);
        CurrentState = state;
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
