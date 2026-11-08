using System.Diagnostics;
using LoqLighting.Backend.Wmi;
using LoqLighting.Core;
using LoqLighting.Core.Effects;

namespace LoqLighting.Probe;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
        {
            PrintUsage();
            return 0;
        }

        string command = args[0].ToLowerInvariant();

        try
        {
            await using var backend = new LenovoWmiBackend();

            return command switch
            {
                "read" => await HandleReadAsync(backend),
                "set" => await HandleSetAsync(backend, args.Length > 1 ? args[1] : null),
                "cycle" => await HandleCycleAsync(backend),
                "probe-off" => await HandleProbeOffAsync(backend),
                "play" => await HandlePlayAsync(backend, args),
                "bench" => await HandleBenchAsync(backend, args),
                _ => HandleUnknownCommand(command)
            };
        }
        catch (LenovoWmiAccessDeniedException ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("[FAIL] Access Denied");
            Console.ResetColor();
            Console.WriteLine(ex.Message);
            Console.WriteLine();
            Console.WriteLine("To run as Administrator:");
            Console.WriteLine("  Start an elevated PowerShell ('Run as administrator') and run the executable.");
            return 1;
        }
        catch (LenovoWmiClassNotFoundException ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("[FAIL] WMI Class Not Found");
            Console.ResetColor();
            Console.WriteLine(ex.Message);
            return 1;
        }
        catch (LenovoWmiDisconnectedException ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("[FAIL] WMI Disconnected");
            Console.ResetColor();
            Console.WriteLine(ex.Message);
            return 1;
        }
        catch (LenovoWmiException ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("[FAIL] WMI Error");
            Console.ResetColor();
            Console.WriteLine(ex.Message);
            return 1;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("[FAIL] Unexpected Error");
            Console.ResetColor();
            Console.WriteLine(ex.Message);
            return 1;
        }
    }

    private static async Task<int> HandleReadAsync(ILightingBackend backend)
    {
        Console.WriteLine("[INFO] Reading current keyboard backlight status...");
        var state = await backend.ReadAsync();

        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write("[PASS] ");
        Console.ResetColor();

        string levelDisplay = state.Level switch
        {
            Brightness.Off => state.RawLevel == 1 ? "Off" : $"Off (unknown raw: {state.RawLevel})",
            _ => state.Level.ToString()
        };

        Console.WriteLine($"Current brightness level: {levelDisplay} (Raw Value: {state.RawLevel}, StateType: {state.StateType})");
        return 0;
    }

    private static async Task<int> HandleSetAsync(ILightingBackend backend, string? levelArg)
    {
        if (string.IsNullOrWhiteSpace(levelArg))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("[FAIL] Missing brightness level for 'set' command.");
            Console.ResetColor();
            Console.WriteLine("Usage: LoqLighting.Probe set low|high");
            return 1;
        }

        string normalized = levelArg.ToLowerInvariant();

        Brightness target;
        switch (normalized)
        {
            case "off":
                target = Brightness.Off;
                break;
            case "low":
                target = Brightness.Low;
                break;
            case "high":
                target = Brightness.High;
                break;
            default:
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[FAIL] Unknown brightness level '{levelArg}'. Supported levels: off, low, high.");
                Console.ResetColor();
                return 1;
        }

        Console.WriteLine($"[INFO] Reading current state before setting {target}...");
        var current = await backend.ReadAsync();

        Console.WriteLine($"[INFO] Setting brightness to {target} (StateType: {current.StateType})...");
        await backend.WriteAsync(new LightingState(target, current.StateType));

        var verified = await backend.ReadAsync();
        if (verified.Level == target)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("[PASS] ");
            Console.ResetColor();
            Console.WriteLine($"Brightness verified as {verified.Level} (Raw Value: {verified.RawLevel}, StateType: {verified.StateType})");
            return 0;
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write("[FAIL] ");
            Console.ResetColor();
            Console.WriteLine($"Expected {target}, but hardware reported {verified.Level} (Raw Value: {verified.RawLevel})");
            return 1;
        }
    }

    private static async Task<int> HandleCycleAsync(ILightingBackend backend)
    {
        Console.WriteLine("[INFO] Starting verification cycle...");
        Console.WriteLine("[INFO] Capturing initial snapshot...");
        var snapshot = await backend.ReadAsync();

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"[INFO] Snapshot captured: Level={snapshot.Level}, RawLevel={snapshot.RawLevel}, StateType={snapshot.StateType}");
        Console.ResetColor();

        bool allPassed = true;

        try
        {
            // Step 1: Set Low and verify
            Console.WriteLine("[TEST 1/2] Setting brightness to Low...");
            await backend.WriteAsync(new LightingState(Brightness.Low, snapshot.StateType));
            await Task.Delay(200);

            var readLow = await backend.ReadAsync();
            if (readLow.Level == Brightness.Low)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("[PASS] ");
                Console.ResetColor();
                Console.WriteLine($"Low level verified (Read: {readLow.Level}, Raw Value: {readLow.RawLevel}, StateType: {readLow.StateType})");
            }
            else
            {
                allPassed = false;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write("[FAIL] ");
                Console.ResetColor();
                Console.WriteLine($"Low level mismatch: expected Low, got {readLow.Level} (Raw: {readLow.RawLevel})");
            }

            // Step 2: Set High and verify
            Console.WriteLine("[TEST 2/2] Setting brightness to High...");
            await backend.WriteAsync(new LightingState(Brightness.High, snapshot.StateType));
            await Task.Delay(200);

            var readHigh = await backend.ReadAsync();
            if (readHigh.Level == Brightness.High)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("[PASS] ");
                Console.ResetColor();
                Console.WriteLine($"High level verified (Read: {readHigh.Level}, Raw Value: {readHigh.RawLevel}, StateType: {readHigh.StateType})");
            }
            else
            {
                allPassed = false;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write("[FAIL] ");
                Console.ResetColor();
                Console.WriteLine($"High level mismatch: expected High, got {readHigh.Level} (Raw: {readHigh.RawLevel})");
            }
        }
        finally
        {
            // Always restore snapshot state
            Console.WriteLine($"[INFO] Restoring snapshot state: Level={snapshot.Level}, RawLevel={snapshot.RawLevel}, StateType={snapshot.StateType}...");
            try
            {
                await backend.RestoreAsync(snapshot);
                var restored = await backend.ReadAsync();

                if (restored.RawLevel == snapshot.RawLevel)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write("[PASS] ");
                    Console.ResetColor();
                    Console.WriteLine($"Restored original state successfully (Level={restored.Level}, Raw Value: {restored.RawLevel})");
                }
                else
                {
                    allPassed = false;
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Write("[FAIL] ");
                    Console.ResetColor();
                    Console.WriteLine($"Failed to verify restored state: expected raw {snapshot.RawLevel}, got raw {restored.RawLevel}");
                }
            }
            catch (Exception ex)
            {
                allPassed = false;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write("[FAIL] ");
                Console.ResetColor();
                Console.WriteLine($"Exception while restoring original state: {ex.Message}");
            }
        }

        Console.WriteLine();
        if (allPassed)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("[PASS] Cycle test passed all checks.");
            Console.ResetColor();
            return 0;
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("[FAIL] One or more checks in the cycle test failed.");
            Console.ResetColor();
            return 1;
        }
    }

    private static async Task<int> HandleProbeOffAsync(LenovoWmiBackend backend)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("================================================================================");
        Console.WriteLine("WARNING: Hardware Probe - Off Value Verification");
        Console.WriteLine("This command will write candidate raw value 1 (Off) to the keyboard lighting");
        Console.WriteLine("controller over ACPI/WMI. While raw 1 was observed via Fn+Space, writing it");
        Console.WriteLine("directly is experimental.");
        Console.WriteLine("The probe will test raw 1, delay 1500 ms, verify read-back, and restore snapshot.");
        Console.WriteLine("================================================================================");
        Console.ResetColor();

        Console.Write("Type 'yes' to proceed: ");
        string? confirmation = Console.ReadLine()?.Trim();
        if (!string.Equals(confirmation, "yes", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("[INFO] Probe cancelled by user.");
            return 0;
        }

        Console.WriteLine("[INFO] Capturing snapshot via ReadAsync before probe...");
        var snapshot = await backend.ReadAsync();

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"[INFO] Initial snapshot: Level={snapshot.Level}, RawLevel={snapshot.RawLevel}, StateType={snapshot.StateType}");
        Console.ResetColor();

        int restoreDone = 0;
        Func<Task> restoreAction = async () =>
        {
            if (Interlocked.Exchange(ref restoreDone, 1) == 0)
            {
                Console.WriteLine($"[INFO] Restoring snapshot state: Level={snapshot.Level}, RawLevel={snapshot.RawLevel}, StateType={snapshot.StateType}...");
                try
                {
                    await backend.RestoreAsync(snapshot, CancellationToken.None);
                    var restored = await backend.ReadAsync(CancellationToken.None);

                    if (restored.RawLevel == snapshot.RawLevel)
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.Write("[PASS] ");
                        Console.ResetColor();
                        Console.WriteLine($"Successfully restored original state (Level={restored.Level}, RawLevel={restored.RawLevel})");
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.Write("[FAIL] ");
                        Console.ResetColor();
                        Console.WriteLine($"Failed to verify restored state: expected raw {snapshot.RawLevel}, got raw {restored.RawLevel}");
                    }
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Write("[FAIL] ");
                    Console.ResetColor();
                    Console.WriteLine($"Exception while restoring original state: {ex.Message}");
                }
            }
        };

        ConsoleCancelEventHandler cancelHandler = (sender, e) =>
        {
            e.Cancel = true;
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("[INFO] Ctrl+C interrupt detected. Restoring snapshot before exit...");
            Console.ResetColor();
            try
            {
                restoreAction().GetAwaiter().GetResult();
            }
            catch
            {
                // Ignore secondary errors on exit
            }
            Environment.Exit(130);
        };

        Console.CancelKeyPress += cancelHandler;

        bool passed = false;
        try
        {
            Console.WriteLine("[TEST] Writing candidate raw value 1 (Off)...");
            await backend.ProbeWriteRawAsync(1, snapshot.StateType);

            Console.WriteLine("[INFO] Delaying 1500 ms to observe hardware state...");
            await Task.Delay(1500);

            Console.WriteLine("[TEST] Reading back backlight status...");
            var readBack = await backend.ReadAsync();

            if (readBack.RawLevel == 1)
            {
                passed = true;
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("[PASS] ");
                Console.ResetColor();
                Console.WriteLine($"Hardware confirmed raw value 1 (Level: {readBack.Level}, RawLevel: {readBack.RawLevel}, StateType: {readBack.StateType})");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write("[FAIL] ");
                Console.ResetColor();
                Console.WriteLine($"Hardware did not confirm raw value 1 (Read: Level={readBack.Level}, RawLevel={readBack.RawLevel}, StateType={readBack.StateType})");
            }
        }
        finally
        {
            Console.CancelKeyPress -= cancelHandler;
            await restoreAction();
        }

        return passed ? 0 : 1;
    }

    private static async Task<int> HandlePlayAsync(ILightingBackend backend, string[] args)
    {
        if (args.Length < 2)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("[FAIL] Missing effect name for 'play' command.");
            Console.ResetColor();
            Console.WriteLine("Usage: LoqLighting.Probe play <effect> [--speed 1.0] [--seconds 10]");
            Console.WriteLine("Available effects: strobe, strobewithpause, breathing, heartbeat, wave, randomflash");
            return 1;
        }

        string effectName = args[1].ToLowerInvariant();
        IEffect? effect = effectName switch
        {
            "strobe" => new StrobeEffect(),
            "strobewithpause" or "strobe-with-pause" => new StrobeWithPauseEffect(),
            "breathing" or "breathe" => new BreathingEffect(),
            "heartbeat" => new HeartbeatEffect(),
            "wave" => new WaveEffect(),
            "randomflash" or "random-flash" => new RandomFlashEffect(),
            _ => null
        };

        if (effect == null)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[FAIL] Unknown effect '{args[1]}'.");
            Console.ResetColor();
            Console.WriteLine("Available effects: strobe, strobewithpause, breathing, heartbeat, wave, randomflash");
            return 1;
        }

        double speed = 1.0;
        TimeSpan? duration = null;

        for (int i = 2; i < args.Length; i++)
        {
            if (args[i].Equals("--speed", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                if (double.TryParse(args[++i], System.Globalization.CultureInfo.InvariantCulture, out double parsedSpeed))
                {
                    speed = parsedSpeed;
                }
            }
            else if ((args[i].Equals("--seconds", StringComparison.OrdinalIgnoreCase) ||
                      args[i].Equals("-s", StringComparison.OrdinalIgnoreCase)) && i + 1 < args.Length)
            {
                if (double.TryParse(args[++i], System.Globalization.CultureInfo.InvariantCulture, out double secs))
                {
                    duration = TimeSpan.FromSeconds(secs);
                }
            }
        }

        var settings = new EffectSettings
        {
            Speed = speed,
            RunDuration = duration
        };

        await using var engine = new LightingEngine(backend);
        engine.Faulted += (s, ex) =>
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n[FAULT] Lighting engine faulted: {ex.Message}");
            Console.ResetColor();
        };

        using var stopCts = new CancellationTokenSource();
        ConsoleCancelEventHandler cancelHandler = (s, e) =>
        {
            e.Cancel = true;
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("[INFO] Ctrl+C interrupt detected. Stopping animation...");
            Console.ResetColor();
            stopCts.Cancel();
        };

        Console.CancelKeyPress += cancelHandler;

        try
        {
            Console.WriteLine($"[INFO] Starting playback of effect '{effect.Name}'...");
            Console.WriteLine($"[INFO] Settings: Speed={settings.Speed}x, Duration={(duration.HasValue ? $"{duration.Value.TotalSeconds}s" : "infinite (Press Ctrl+C to stop)")}");

            await engine.StartAsync(effect, settings, stopCts.Token);

            while (engine.IsRunning && !stopCts.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(100, stopCts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
        finally
        {
            Console.CancelKeyPress -= cancelHandler;
            Console.WriteLine("[INFO] Stopping engine and restoring original brightness...");
            await engine.StopAsync();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("[PASS] Effect stopped and original brightness restored successfully.");
            Console.ResetColor();
        }

        return 0;
    }

    private static async Task<int> HandleBenchAsync(ILightingBackend backend, string[] args)
    {
        int durationSeconds = 30;
        for (int i = 1; i < args.Length; i++)
        {
            if ((args[i].Equals("--seconds", StringComparison.OrdinalIgnoreCase) ||
                 args[i].Equals("-s", StringComparison.OrdinalIgnoreCase)) && i + 1 < args.Length)
            {
                if (int.TryParse(args[++i], out int parsedSec) && parsedSec > 0)
                {
                    durationSeconds = parsedSec;
                }
            }
        }

        Console.WriteLine("============================================================");
        Console.WriteLine("LoqLighting Hardware Benchmark (Strobe: 200ms, 100ms, 50ms)");
        Console.WriteLine("============================================================");
        Console.WriteLine($"Duration per tier: {durationSeconds} seconds");
        Console.WriteLine("Interruption: Press Ctrl+C at any time to abort and restore original brightness.\n");

        Console.WriteLine("[INFO] Reading initial keyboard backlight state...");
        var initialSnapshot = await backend.ReadAsync();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write("[PASS] ");
        Console.ResetColor();
        Console.WriteLine($"Initial state captured: Level={initialSnapshot.Level} (Raw Value: {initialSnapshot.RawLevel}, StateType: {initialSnapshot.StateType})\n");

        var decorator = new BenchmarkingBackendDecorator(backend);
        await using var engine = new LightingEngine(decorator);

        engine.Faulted += (s, ex) =>
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n[FAULT] Lighting engine faulted during benchmark: {ex.Message}");
            Console.ResetColor();
        };

        using var stopCts = new CancellationTokenSource();
        ConsoleCancelEventHandler cancelHandler = (s, e) =>
        {
            e.Cancel = true;
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("[INFO] Ctrl+C interrupt detected. Cancelling benchmark...");
            Console.ResetColor();
            stopCts.Cancel();
        };

        Console.CancelKeyPress += cancelHandler;

        int[] intervalsMs = [200, 100, 50];
        var results = new List<BenchTierResult>();
        var strobe = new StrobeEffect();

        try
        {
            foreach (int intervalMs in intervalsMs)
            {
                if (stopCts.IsCancellationRequested)
                {
                    break;
                }

                double speed = 100.0 / intervalMs;
                var settings = new EffectSettings
                {
                    Speed = speed,
                    RunDuration = TimeSpan.FromSeconds(durationSeconds)
                };

                Console.WriteLine($"[INFO] Starting Tier: {intervalMs} ms step interval (Speed={speed:F2}x, Target Duration={durationSeconds}s)...");

                decorator.ResetMetrics();
                var process = Process.GetCurrentProcess();
                var cpuStart = process.TotalProcessorTime;
                var wallSw = Stopwatch.StartNew();

                decorator.IsRecording = true;
                await engine.StartAsync(strobe, settings, stopCts.Token);

                while (engine.IsRunning && !stopCts.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(100, stopCts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }

                await engine.StopAsync();
                decorator.IsRecording = false;
                wallSw.Stop();

                var cpuEnd = process.TotalProcessorTime;
                double cpuUsedMs = (cpuEnd - cpuStart).TotalMilliseconds;
                double wallTimeMs = wallSw.Elapsed.TotalMilliseconds;
                double totalCpuPercent = wallTimeMs > 0 ? (cpuUsedMs / (wallTimeMs * Environment.ProcessorCount)) * 100.0 : 0.0;
                double singleCoreCpuPercent = wallTimeMs > 0 ? (cpuUsedMs / wallTimeMs) * 100.0 : 0.0;

                var durations = decorator.CallDurationsMs;
                int calls = durations.Count;
                int errors = decorator.ErrorCount;
                double avgCallMs = calls > 0 ? durations.Average() : 0.0;
                double maxCallMs = calls > 0 ? durations.Max() : 0.0;
                double minCallMs = calls > 0 ? durations.Min() : 0.0;

                var tierResult = new BenchTierResult(
                    intervalMs,
                    calls,
                    avgCallMs,
                    maxCallMs,
                    minCallMs,
                    errors,
                    totalCpuPercent,
                    singleCoreCpuPercent);

                results.Add(tierResult);

                Console.WriteLine($"------------------------------------------------------------");
                Console.WriteLine($"Tier Results: {intervalMs} ms step interval ({durationSeconds} s)");
                Console.WriteLine($"------------------------------------------------------------");
                Console.WriteLine($"  WMI Calls Completed : {calls}");
                Console.WriteLine($"  Average Call Time   : {avgCallMs:F2} ms");
                Console.WriteLine($"  Max Call Time       : {maxCallMs:F2} ms");
                Console.WriteLine($"  Min Call Time       : {minCallMs:F2} ms");
                Console.WriteLine($"  Error Count         : {errors}");
                Console.WriteLine($"  Process CPU Usage   : {totalCpuPercent:F2}% (Total system across {Environment.ProcessorCount} cores)");
                Console.WriteLine($"                        {singleCoreCpuPercent:F2}% (Single core equivalent)");
                Console.WriteLine($"------------------------------------------------------------\n");

                if (!stopCts.IsCancellationRequested && intervalMs != intervalsMs[^1])
                {
                    Console.WriteLine("[INFO] Resting for 1 second before next tier...\n");
                    try
                    {
                        await Task.Delay(1000, stopCts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }

            if (results.Count > 0)
            {
                Console.WriteLine("========================================================================================================");
                Console.WriteLine("Benchmark Summary");
                Console.WriteLine("========================================================================================================");
                Console.WriteLine($"{"Step Hold",9} | {"Calls",7} | {"Avg WMI Time",12} | {"Max WMI Time",12} | {"Errors",6} | {"CPU (Total)",11} | {"CPU (1-Core)",12}");
                Console.WriteLine("--------------------------------------------------------------------------------------------------------");
                foreach (var r in results)
                {
                    Console.WriteLine($"{r.IntervalMs,7} ms | {r.Calls,7} | {r.AvgCallTimeMs,9:F2} ms | {r.MaxCallTimeMs,9:F2} ms | {r.ErrorCount,6} | {r.TotalCpuPercent,10:F2}% | {r.SingleCoreCpuPercent,11:F2}%");
                }
                Console.WriteLine("========================================================================================================\n");
            }
        }
        finally
        {
            Console.CancelKeyPress -= cancelHandler;
            Console.WriteLine("[INFO] Restoring original backlight state...");
            try
            {
                await backend.RestoreAsync(initialSnapshot, CancellationToken.None);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("[PASS] ");
                Console.ResetColor();
                Console.WriteLine($"Original brightness restored: Level={initialSnapshot.Level} (Raw Value: {initialSnapshot.RawLevel}, StateType: {initialSnapshot.StateType})");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write("[FAIL] ");
                Console.ResetColor();
                Console.WriteLine($"Failed to restore original backlight state: {ex.Message}");
            }
        }

        return stopCts.IsCancellationRequested ? 1 : 0;
    }

    private readonly record struct BenchTierResult(
        int IntervalMs,
        int Calls,
        double AvgCallTimeMs,
        double MaxCallTimeMs,
        double MinCallTimeMs,
        int ErrorCount,
        double TotalCpuPercent,
        double SingleCoreCpuPercent);

    private sealed class BenchmarkingBackendDecorator : ILightingBackend
    {
        private readonly ILightingBackend _inner;
        private readonly List<double> _callDurationsMs = new();
        private readonly object _lock = new();

        public bool IsRecording { get; set; }
        public int ErrorCount { get; private set; }

        public IReadOnlyList<double> CallDurationsMs
        {
            get
            {
                lock (_lock)
                {
                    return _callDurationsMs.ToArray();
                }
            }
        }

        public BenchmarkingBackendDecorator(ILightingBackend inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public IReadOnlySet<Brightness> SupportedLevels => _inner.SupportedLevels;

        public Task<LightingState> ReadAsync(CancellationToken ct = default) => _inner.ReadAsync(ct);

        public async Task WriteAsync(LightingState state, CancellationToken ct = default)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                await _inner.WriteAsync(state, ct).ConfigureAwait(false);
            }
            catch
            {
                if (IsRecording)
                {
                    lock (_lock)
                    {
                        ErrorCount++;
                    }
                }
                throw;
            }
            finally
            {
                sw.Stop();
                if (IsRecording)
                {
                    lock (_lock)
                    {
                        _callDurationsMs.Add(sw.Elapsed.TotalMilliseconds);
                    }
                }
            }
        }

        public Task RestoreAsync(LightingState state, CancellationToken ct = default)
        {
            return _inner.RestoreAsync(state, ct);
        }

        public void ResetMetrics()
        {
            lock (_lock)
            {
                _callDurationsMs.Clear();
                ErrorCount = 0;
            }
        }

        public ValueTask DisposeAsync() => _inner.DisposeAsync();
    }

    private static int HandleUnknownCommand(string command)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[FAIL] Unknown command '{command}'.");
        Console.ResetColor();
        PrintUsage();
        return 1;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("LoqLighting Probe - Lenovo LOQ Backlight Hardware Verification");
        Console.WriteLine("Usage:");
        Console.WriteLine("  LoqLighting.Probe read           - Read current backlight brightness and state type");
        Console.WriteLine("  LoqLighting.Probe set low|high   - Set backlight brightness to low or high");
        Console.WriteLine("  LoqLighting.Probe cycle          - Snapshot state, test Low, test High, restore original");
        Console.WriteLine("  LoqLighting.Probe probe-off      - Experimental probe of candidate raw value 1 (Off)");
        Console.WriteLine("  LoqLighting.Probe play <effect>  - Play an effect [--speed 1.0] [--seconds 10]");
        Console.WriteLine("  LoqLighting.Probe bench          - Benchmark Strobe at 200/100/50 ms for 30s each [--seconds 30]");
    }
}
