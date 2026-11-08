using LoqLighting.Backend.Wmi;
using LoqLighting.Core;
using Xunit;

namespace LoqLighting.Core.Tests;

public class LenovoWmiBackendTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(255)]
    public async Task RestoreAsync_WithUnverifiedRawLevel_ThrowsArgumentOutOfRangeException(byte unverifiedRaw)
    {
        await using var backend = new LenovoWmiBackend();
        var unverifiedState = new LightingState(Brightness.Off, 0, unverifiedRaw);

        var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => backend.RestoreAsync(unverifiedState));

        Assert.Equal("state", ex.ParamName);
        Assert.Equal(unverifiedRaw, ex.ActualValue);
    }

    [Theory]
    [InlineData(Brightness.Off, (byte)1)]
    [InlineData(Brightness.Low, (byte)2)]
    [InlineData(Brightness.High, (byte)3)]
    public async Task RestoreAsync_WithVerifiedRawLevel_Succeeds(Brightness level, byte rawLevel)
    {
        await using var backend = new LenovoWmiBackend();
        byte? writtenRaw = null;
        byte? writtenState = null;

        backend.WmiWriter = (raw, state) =>
        {
            writtenRaw = raw;
            writtenState = state;
        };

        var validState = new LightingState(level, 0, rawLevel);
        await backend.RestoreAsync(validState);

        Assert.Equal(rawLevel, writtenRaw);
        Assert.Equal((byte)0, writtenState);
    }

    [Fact]
    public async Task SemaphoreSerializes_WmiCalls()
    {
        await using var backend = new LenovoWmiBackend();
        int activeCalls = 0;
        int maxConcurrent = 0;
        var sync = new object();

        backend.WmiWriter = (raw, state) =>
        {
            lock (sync)
            {
                activeCalls++;
                if (activeCalls > maxConcurrent)
                {
                    maxConcurrent = activeCalls;
                }
            }

            Thread.Sleep(50);

            lock (sync)
            {
                activeCalls--;
            }
        };

        var task1 = backend.WriteAsync(new LightingState(Brightness.Low, 0, 2));
        var task2 = backend.WriteAsync(new LightingState(Brightness.High, 0, 3));
        var task3 = backend.ProbeWriteRawAsync(1, 0);

        await Task.WhenAll(task1, task2, task3);

        Assert.Equal(1, maxConcurrent);
    }

    [Fact]
    public async Task SemaphoreReleasedInFinally_WhenWmiThrows()
    {
        await using var backend = new LenovoWmiBackend();

        backend.WmiWriter = (raw, state) => throw new InvalidOperationException("WMI failure");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => backend.WriteAsync(new LightingState(Brightness.Low, 0, 2)));

        // Gate should be 1 (released)
        Assert.Equal(1, backend.WmiGate.CurrentCount);

        // Next call can acquire the gate immediately
        bool secondCallExecuted = false;
        backend.WmiWriter = (raw, state) => secondCallExecuted = true;

        await backend.WriteAsync(new LightingState(Brightness.High, 0, 3));
        Assert.True(secondCallExecuted);
        Assert.Equal(1, backend.WmiGate.CurrentCount);
    }
}
