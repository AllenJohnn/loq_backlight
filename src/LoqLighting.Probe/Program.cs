using LoqLighting.Backend.Wmi;
using LoqLighting.Core;

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

        if (normalized == "off")
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("[FAIL] 'Off' brightness level is NOT verified on this hardware.");
            Console.ResetColor();
            Console.WriteLine("Off remains disabled to prevent sending unverified codes. Supported levels: low, high.");
            return 1;
        }

        Brightness target;
        switch (normalized)
        {
            case "low":
                target = Brightness.Low;
                break;
            case "high":
                target = Brightness.High;
                break;
            default:
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[FAIL] Unknown brightness level '{levelArg}'. Supported levels: low, high.");
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
            Console.WriteLine($"Brightness verified as {verified.Level} (StateType: {verified.StateType})");
            return 0;
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write("[FAIL] ");
            Console.ResetColor();
            Console.WriteLine($"Expected {target}, but hardware reported {verified.Level}");
            return 1;
        }
    }

    private static async Task<int> HandleCycleAsync(ILightingBackend backend)
    {
        Console.WriteLine("[INFO] Starting verification cycle...");
        Console.WriteLine("[INFO] Capturing initial snapshot...");
        var snapshot = await backend.ReadAsync();

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"[INFO] Snapshot captured: Level={snapshot.Level}, StateType={snapshot.StateType}");
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
                Console.WriteLine($"Low level verified (Read: {readLow.Level}, StateType: {readLow.StateType})");
            }
            else
            {
                allPassed = false;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write("[FAIL] ");
                Console.ResetColor();
                Console.WriteLine($"Low level mismatch: expected Low, got {readLow.Level}");
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
                Console.WriteLine($"High level verified (Read: {readHigh.Level}, StateType: {readHigh.StateType})");
            }
            else
            {
                allPassed = false;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write("[FAIL] ");
                Console.ResetColor();
                Console.WriteLine($"High level mismatch: expected High, got {readHigh.Level}");
            }
        }
        finally
        {
            // Always restore snapshot state
            Console.WriteLine($"[INFO] Restoring snapshot state: Level={snapshot.Level}, StateType={snapshot.StateType}...");
            try
            {
                await backend.WriteAsync(snapshot);
                var restored = await backend.ReadAsync();

                if (restored.Level == snapshot.Level)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write("[PASS] ");
                    Console.ResetColor();
                    Console.WriteLine($"Restored original state successfully (Level={restored.Level})");
                }
                else
                {
                    allPassed = false;
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Write("[FAIL] ");
                    Console.ResetColor();
                    Console.WriteLine($"Failed to verify restored state: expected {snapshot.Level}, got {restored.Level}");
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
    }
}
