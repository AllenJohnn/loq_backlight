using System.Diagnostics;

namespace LoqLighting.Core;

public class LightingEngine : IAsyncDisposable
{
    public static readonly TimeSpan MinimumHold = TimeSpan.FromMilliseconds(50);
    private const int MaxWmiRetries = 3;

    private readonly ILightingBackend _backend;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private CancellationTokenSource? _cts;
    private Task? _loopTask;
    private bool _disposed;

    public event EventHandler<Exception>? Faulted;

    public bool IsRunning => _loopTask != null && !_loopTask.IsCompleted;

    public LightingEngine(ILightingBackend backend)
    {
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
    }

    public async Task StartAsync(IEffect effect, EffectSettings? settings = null, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(effect);

        settings ??= new EffectSettings();

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // If already running, stop previous run first
            await StopInternalAsync().ConfigureAwait(false);

            _cts = new CancellationTokenSource();
            var runCts = _cts;

            // Capture initial snapshot before starting animation
            LightingState snapshot;
            try
            {
                snapshot = await _backend.ReadAsync(runCts.Token).ConfigureAwait(false);
            }
            catch
            {
                runCts.Cancel();
                runCts.Dispose();
                _cts = null;
                throw;
            }

            _loopTask = Task.Run(() => RunLoopAsync(effect, settings, snapshot, runCts.Token));
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task StopAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            await StopInternalAsync().ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task StopInternalAsync()
    {
        if (_cts != null)
        {
            _cts.Cancel();
        }

        if (_loopTask != null)
        {
            try
            {
                await _loopTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected on cancellation
            }
            catch
            {
                // Unhandled loop exceptions handled via Faulted
            }
            finally
            {
                _loopTask = null;
                _cts?.Dispose();
                _cts = null;
            }
        }
    }

    private async Task RunLoopAsync(
        IEffect effect,
        EffectSettings settings,
        LightingState snapshot,
        CancellationToken ct)
    {
        using var timerScope = TimerResolution.Request(1);

        Brightness? lastLevel = snapshot.Level;
        Exception? faultException = null;
        var stopwatch = Stopwatch.StartNew();
        var nextDeadline = TimeSpan.Zero;

        try
        {
            foreach (var step in effect.Generate(settings))
            {
                ct.ThrowIfCancellationRequested();

                if (settings.RunDuration.HasValue && stopwatch.Elapsed >= settings.RunDuration.Value)
                {
                    break;
                }

                // Clamp hold to at least 50 ms
                var hold = step.Hold < MinimumHold ? MinimumHold : step.Hold;
                nextDeadline += hold;

                // Skip redundant writes
                if (lastLevel != step.Level)
                {
                    int retries = 0;
                    while (true)
                    {
                        ct.ThrowIfCancellationRequested();

                        try
                        {
                            await _backend.WriteAsync(new LightingState(step.Level, snapshot.StateType), ct).ConfigureAwait(false);
                            lastLevel = step.Level;
                            break;
                        }
                        catch (OperationCanceledException)
                        {
                            throw;
                        }
                        catch (Exception ex)
                        {
                            retries++;
                            if (retries > MaxWmiRetries)
                            {
                                faultException = ex;
                                throw;
                            }

                            // Retry with backoff: 50ms, 100ms, 200ms
                            int backoffMs = 50 * (1 << (retries - 1));
                            await Task.Delay(backoffMs, ct).ConfigureAwait(false);
                        }
                    }
                }

                // Prevent deadline from lagging behind real time if a stall occurred
                if (nextDeadline < stopwatch.Elapsed)
                {
                    nextDeadline = stopwatch.Elapsed;
                }

                var targetDeadline = nextDeadline;
                bool isFinalDelay = false;
                if (settings.RunDuration.HasValue)
                {
                    if (stopwatch.Elapsed >= settings.RunDuration.Value)
                    {
                        break;
                    }

                    if (settings.RunDuration.Value <= targetDeadline)
                    {
                        targetDeadline = settings.RunDuration.Value;
                        isFinalDelay = true;
                    }
                }

                var delay = targetDeadline - stopwatch.Elapsed;
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, ct).ConfigureAwait(false);
                }

                if (isFinalDelay || (settings.RunDuration.HasValue && stopwatch.Elapsed >= settings.RunDuration.Value))
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Clean cancellation
        }
        catch (Exception ex) when (faultException != null || ex is not OperationCanceledException)
        {
            faultException ??= ex;
        }
        finally
        {
            // Stop cancels, awaits loop exit, then RestoreAsync(original snapshot) in finally
            try
            {
                await _backend.RestoreAsync(snapshot, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                faultException = faultException != null ? new AggregateException(faultException, ex) : ex;
            }

            if (faultException != null)
            {
                Faulted?.Invoke(this, faultException);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            await StopAsync().ConfigureAwait(false);
            _gate.Dispose();
            _disposed = true;
        }

        GC.SuppressFinalize(this);
    }
}
