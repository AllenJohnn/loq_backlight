using System.Diagnostics;
using LoqLighting.Core;
using Xunit;

namespace LoqLighting.Core.Tests;

public class LightingEngineTests
{
    private class SimpleEffect : IEffect
    {
        private readonly IEnumerable<Step> _steps;
        public string Name => "TestSimpleEffect";

        public SimpleEffect(params Step[] steps)
        {
            _steps = steps;
        }

        public SimpleEffect(IEnumerable<Step> steps)
        {
            _steps = steps;
        }

        public IEnumerable<Step> Generate(EffectSettings settings) => _steps;
    }

    private class InfiniteEffect : IEffect
    {
        public string Name => "InfiniteEffect";

        public IEnumerable<Step> Generate(EffectSettings settings)
        {
            while (true)
            {
                yield return new Step(Brightness.Low, TimeSpan.FromMilliseconds(50));
                yield return new Step(Brightness.High, TimeSpan.FromMilliseconds(50));
            }
        }
    }

    [Fact]
    public async Task NoOverlappingRuns_StartingNewRun_StopsPreviousRunFirst()
    {
        var backend = new FakeBackend { CurrentState = new LightingState(Brightness.High, 0, 3) };
        await using var engine = new LightingEngine(backend);

        var effect1 = new InfiniteEffect();
        var effect2 = new InfiniteEffect();

        await engine.StartAsync(effect1);
        Assert.True(engine.IsRunning);

        // Starting run 2 must stop run 1 first
        await engine.StartAsync(effect2);
        Assert.True(engine.IsRunning);

        await engine.StopAsync();
        Assert.False(engine.IsRunning);
    }

    [Fact]
    public async Task StopAsync_RestoresOriginalState()
    {
        var originalState = new LightingState(Brightness.High, 0, 3);
        var backend = new FakeBackend { CurrentState = originalState };
        await using var engine = new LightingEngine(backend);

        var effect = new SimpleEffect(
            new Step(Brightness.Low, TimeSpan.FromMilliseconds(60)),
            new Step(Brightness.Off, TimeSpan.FromMilliseconds(60)),
            new Step(Brightness.Low, TimeSpan.FromMilliseconds(60))
        );

        await engine.StartAsync(effect);
        await Task.Delay(80); // Allow first step to execute
        await engine.StopAsync();

        Assert.True(backend.RestoreCallCount >= 1);
        Assert.Equal(Brightness.High, backend.RestoredStates[^1].Level);
    }

    [Fact]
    public async Task StopAsync_IsPrompt()
    {
        var backend = new FakeBackend();
        await using var engine = new LightingEngine(backend);

        var effect = new InfiniteEffect();
        await engine.StartAsync(effect);

        var sw = Stopwatch.StartNew();
        await engine.StopAsync();
        sw.Stop();

        Assert.False(engine.IsRunning);
        Assert.True(sw.ElapsedMilliseconds < 500, $"Stop took too long: {sw.ElapsedMilliseconds} ms");
    }

    [Fact]
    public async Task RedundantWritesSkipped()
    {
        // Initial state is Low
        var backend = new FakeBackend { CurrentState = new LightingState(Brightness.Low, 0, 2) };
        await using var engine = new LightingEngine(backend);

        // Steps contain redundant Lows and redundant Highs
        var effect = new SimpleEffect(
            new Step(Brightness.Low, TimeSpan.FromMilliseconds(50)),
            new Step(Brightness.Low, TimeSpan.FromMilliseconds(50)),
            new Step(Brightness.High, TimeSpan.FromMilliseconds(50)),
            new Step(Brightness.High, TimeSpan.FromMilliseconds(50))
        );

        await engine.StartAsync(effect);
        // Wait for steps to complete
        await Task.Delay(350);
        await engine.StopAsync();

        // Since initial state is Low, the first two Lows are redundant (0 writes).
        // The transition to High is 1 write.
        // The second High is redundant (0 writes).
        // Total writes during animation should be 1.
        Assert.Single(backend.WrittenStates);
        Assert.Equal(Brightness.High, backend.WrittenStates[0].Level);
    }

    [Fact]
    public async Task FaultedBackend_StopsRun_Restores_AndRaisesFaultedEvent()
    {
        var backend = new FakeBackend
        {
            CurrentState = new LightingState(Brightness.High, 0, 3),
            ThrowOnWrite = true,
            WriteException = new InvalidOperationException("WMI hardware failure")
        };

        await using var engine = new LightingEngine(backend);

        var tcs = new TaskCompletionSource<Exception>();
        engine.Faulted += (sender, ex) => tcs.TrySetResult(ex);

        var effect = new SimpleEffect(new Step(Brightness.Low, TimeSpan.FromMilliseconds(50)));
        await engine.StartAsync(effect);

        // Wait for retries and Faulted event
        var completed = await Task.WhenAny(tcs.Task, Task.Delay(2000));
        Assert.Same(tcs.Task, completed);

        var ex = await tcs.Task;
        Assert.Equal("WMI hardware failure", ex.Message);
        Assert.False(engine.IsRunning);
        Assert.True(backend.RestoreCallCount >= 1);
    }

    [Fact]
    public async Task RestoreAsyncFailure_RaisesFaultedEvent()
    {
        var expectedEx = new InvalidOperationException("Restore failure");
        var backend = new FakeBackend
        {
            CurrentState = new LightingState(Brightness.High, 0, 3),
            ThrowOnRestore = true,
            RestoreException = expectedEx
        };

        await using var engine = new LightingEngine(backend);

        var tcs = new TaskCompletionSource<Exception>();
        engine.Faulted += (sender, ex) => tcs.TrySetResult(ex);

        var effect = new SimpleEffect(new Step(Brightness.Low, TimeSpan.FromMilliseconds(50)));
        await engine.StartAsync(effect);

        await engine.StopAsync();

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(2000));
        Assert.Same(tcs.Task, completed);

        var ex = await tcs.Task;
        Assert.Same(expectedEx, ex);
    }
}
