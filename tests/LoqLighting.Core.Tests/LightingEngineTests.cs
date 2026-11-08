using System.Diagnostics;
using LoqLighting.Core;
using LoqLighting.Core.Effects;
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

    [Fact]
    public async Task RunDuration_EndsPromptly_DelayingMinHoldAndRemaining()
    {
        var backend = new FakeBackend { CurrentState = new LightingState(Brightness.High, 0, 3) };
        await using var engine = new LightingEngine(backend);

        // Single step with large hold (1000 ms), but RunDuration is 100 ms
        var effect = new SimpleEffect(new Step(Brightness.Low, TimeSpan.FromMilliseconds(1000)));
        var settings = new EffectSettings
        {
            RunDuration = TimeSpan.FromMilliseconds(100)
        };

        var sw = Stopwatch.StartNew();
        await engine.StartAsync(effect, settings);

        while (engine.IsRunning)
        {
            await Task.Delay(20);
        }
        sw.Stop();

        // Should complete around 100 ms, definitely far less than 600 ms
        Assert.True(sw.ElapsedMilliseconds >= 70, $"Ended too early: {sw.ElapsedMilliseconds} ms");
        Assert.True(sw.ElapsedMilliseconds < 500, $"Did not end at RunDuration: {sw.ElapsedMilliseconds} ms");
        Assert.True(backend.RestoreCallCount >= 1);
    }

    [Fact]
    public async Task StartAsync_InitialReadThrows_DisposesCtsAndSetsNull()
    {
        var expectedEx = new InvalidOperationException("Initial read failed");
        var backend = new FakeBackend
        {
            ThrowOnRead = true,
            ReadException = expectedEx
        };

        await using var engine = new LightingEngine(backend);
        var effect = new SimpleEffect(new Step(Brightness.Low, TimeSpan.FromMilliseconds(50)));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => engine.StartAsync(effect));
        Assert.Same(expectedEx, ex);

        // Engine should not be running
        Assert.False(engine.IsRunning);

        // Subsequent StopAsync should succeed without throwing ObjectDisposedException
        await engine.StopAsync();

        // Engine can successfully start when read recovers
        backend.ThrowOnRead = false;
        await engine.StartAsync(effect);
        Assert.True(engine.IsRunning);
        await engine.StopAsync();
    }

    [Fact]
    public void TimerResolution_RequestsAndDisposesWithoutException()
    {
        using (var scope = TimerResolution.Request(1))
        {
            Assert.NotNull(scope);
        }
    }

    [Fact]
    public async Task TimelineScheduling_DoesNotDrift()
    {
        var backend = new FakeBackend { CurrentState = new LightingState(Brightness.High, 0, 3) };
        await using var engine = new LightingEngine(backend);

        // 6 steps of 50 ms each = 300 ms timeline
        var steps = new List<Step>();
        for (int i = 0; i < 6; i++)
        {
            steps.Add(new Step(i % 2 == 0 ? Brightness.Low : Brightness.High, TimeSpan.FromMilliseconds(50)));
        }

        var effect = new SimpleEffect(steps);
        var sw = Stopwatch.StartNew();
        await engine.StartAsync(effect);

        while (engine.IsRunning)
        {
            await Task.Delay(20);
        }
        sw.Stop();

        // With absolute scheduling, 6 x 50 ms steps should finish very close to 300 ms, well under 450 ms
        Assert.True(sw.ElapsedMilliseconds >= 250, $"Elapsed too short: {sw.ElapsedMilliseconds} ms");
        Assert.True(sw.ElapsedMilliseconds < 450, $"Drifted too much: {sw.ElapsedMilliseconds} ms");
        Assert.True(backend.RestoreCallCount >= 1);
    }

    [Fact]
    public async Task BenchmarkTiers_ExecuteExpectedCallRatesWithoutDrift()
    {
        var backend = new FakeBackend { CurrentState = new LightingState(Brightness.High, 0, 3) };
        await using var engine = new LightingEngine(backend);
        var strobe = new StrobeEffect();

        // 200 ms tier for 1000 ms -> ~5 steps/sec
        var settings200 = new EffectSettings { Speed = 0.5, RunDuration = TimeSpan.FromMilliseconds(1000) };
        await engine.StartAsync(strobe, settings200);
        while (engine.IsRunning) await Task.Delay(20);
        await engine.StopAsync();
        int calls200 = backend.WrittenStates.Count;

        backend.WrittenStates.Clear();

        // 100 ms tier for 1000 ms -> ~10 steps/sec
        var settings100 = new EffectSettings { Speed = 1.0, RunDuration = TimeSpan.FromMilliseconds(1000) };
        await engine.StartAsync(strobe, settings100);
        while (engine.IsRunning) await Task.Delay(20);
        await engine.StopAsync();
        int calls100 = backend.WrittenStates.Count;

        backend.WrittenStates.Clear();

        // 50 ms tier for 1000 ms -> ~20 steps/sec
        var settings50 = new EffectSettings { Speed = 2.0, RunDuration = TimeSpan.FromMilliseconds(1000) };
        await engine.StartAsync(strobe, settings50);
        while (engine.IsRunning) await Task.Delay(20);
        await engine.StopAsync();
        int calls50 = backend.WrittenStates.Count;

        // Verify call counts per second match expected rates without drift:
        // 200 ms: ~4-5 writes/sec (150 in 30s)
        // 100 ms: ~9-10 writes/sec (300 in 30s)
        // 50 ms: ~19-20 writes/sec (600 in 30s)
        Assert.InRange(calls200, 4, 6);
        Assert.InRange(calls100, 9, 11);
        Assert.InRange(calls50, 19, 21);
    }
}
