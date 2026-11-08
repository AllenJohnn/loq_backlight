# Architecture (Phase 1 proposal)

Status: proposal. Items marked **[unverified]** must be confirmed on the target hardware before code depends on them.

## 1. Corrections to the handoff

1. **Off value is unverified.** The handoff lists `1 = Off, 2 = Low, 3 = High`, but only levels 2 and 3 were observed over WMI on Windows. The Linux sysfs interface for this keyboard is commonly 0-2, so the `1 = Off` numbering may be inherited from a different mapping. Probe before use (section 5).
2. **"~115 calls/s" is not an animation rate.** Call duration is not LED latency. Plan for step intervals of 50 ms or slower until measured.
3. **Breathing/heartbeat/wave are stepped patterns over 3 levels**, not fades. With one zone, "wave" has no spatial meaning; it can only be a rhythmic sequence. Name and describe effects honestly in the UI.
4. **Both reference repos could not be inspected in this session** (Ninad2003/Keyboard_Effect_LenovoLOQ and Thrash-PRX/Thrash-Lightening-Control returned fetch errors). Nothing here is claimed from them. Re-check once readable.

## 2. API choice **[unverified on target]**

| Option | Notes |
|---|---|
| `System.Management` (NuGet on modern .NET, Windows only) | Familiar `ManagementObject.InvokeMethod`; works for instance methods in `root\WMI`. Legacy but maintained. |
| `Microsoft.Management.Infrastructure` (CIM) | Modern WMI client; more verbose; better async story. |

Recommendation: start with `System.Management` behind `ILightingBackend`. Switching later is a one-class change.

## 3. Administrator rights

**Measured:** non-elevated PowerShell gets `Access denied` when opening `root\WMI` / `LENOVO_LIGHTING_METHOD`. Elevated behaviour is still to be confirmed.

Design consequences:

- **Phase 2-4:** the Probe CLI and app run elevated (`requireAdministrator` in the app manifest). Simple and honest.
- **Later option:** split into an unelevated UI plus a small elevated background service/scheduled task, talking over a named pipe. This also fixes "auto-start at login" (a `requireAdministrator` app cannot start from the normal Run key without a UAC prompt; use a scheduled task with highest privileges instead). Worth the complexity only after the core works.

Original test, kept for reference:

```powershell
$wmi = Get-WmiObject -Namespace "root\WMI" -Class "LENOVO_LIGHTING_METHOD"
$wmi.Get_Lighting_Current_Status([byte]0) | Format-List *
```

Then repeat the Set test from an elevated and a non-elevated shell. Record results in `docs/hardware-findings.md`. Design the app to run unelevated unless this proves impossible.

## 4. Project layout

```text
LoqLightingStudio.sln
src/
  LoqLighting.Core/            interfaces, models, effect engine (no WMI, no UI)
  LoqLighting.Backend.Wmi/     Lenovo WMI implementation of ILightingBackend
  LoqLighting.Probe/           console tool for hardware validation and benchmarks
  LoqLighting.App/             WPF UI, tray, profiles, startup
tests/
  LoqLighting.Core.Tests/      effect timing/cancellation tests using a fake backend
docs/
```

Dependency direction: `App -> Core <- Backend.Wmi`. `Core` never references `System.Management`.

### Core contracts (sketch)

```csharp
public enum Brightness : byte { Off, Low, High }

public readonly record struct LightingState(Brightness Level, byte StateType);

public interface ILightingBackend : IAsyncDisposable
{
    Task<LightingState> ReadAsync(CancellationToken ct);
    Task WriteAsync(LightingState state, CancellationToken ct);
    IReadOnlySet<Brightness> SupportedLevels { get; }
}
```

The backend maps `Brightness` to raw bytes internally. Callers can never send arbitrary raw values.

## 5. Safety rules (from the non-negotiables)

- **Whitelist raw values.** Backend accepts only levels it has confirmed. Off stays disabled until probed.
- **Off-value probe:** read current state, write candidate Off value, read back, restore original, in one guarded routine with `try/finally`. Run manually from `LoqLighting.Probe`, one candidate at a time, and record the result.
- **Snapshot and restore:** capture state at start; restore in `finally`, on Stop, on app exit, and on unhandled exception.
- **Single writer:** all WMI calls go through one serialized queue (one consumer). No two animation loops can write concurrently.
- **Stop is a cancellation, not a flag:** one `CancellationTokenSource` per run; Stop cancels, awaits loop exit, then restores.

## 6. Threading and timing

- One dedicated animation loop (`Task` with `PeriodicTimer` or `Stopwatch`-corrected delays). Default Windows timer granularity is about 15.6 ms, so sub-20 ms steps are not meaningful.
- WMI calls run off the UI thread. Failures are retried with backoff a bounded number of times, then surfaced as a `Faulted` status.
- Skip redundant writes: do not write if the target level equals the last written level.
- Handle `SystemEvents.PowerModeChanged`: on resume, re-read state, then resume or stop the effect. Behaviour after resume is **[unverified]**.
- Lenovo Vantage or the Fn key may change brightness externally **[unverified]**. Read before write at low frequency to detect drift.

## 7. Effects model

An effect is a pure sequence generator: `IEnumerable<Step>` where `Step = (Brightness level, TimeSpan hold)`. The engine runs steps through the backend. This keeps effects unit-testable with a fake backend and no hardware.

First effects: Strobe, Strobe with pause, Breathing (Off/Low/High/Low cycle), Heartbeat (double pulse + rest), Wave (rhythmic sequence), Random flash.

Reactive typing needs a low-level keyboard hook. It must react to key events only and never record key content. Defer to a later phase; antivirus products may flag hooks.

## 8. Phased plan

1. Phase 1: this document, repo scaffold. 
2. Phase 2: `Core` contracts, `Backend.Wmi`, `Probe` (read, set Low/High, verify, restore). Needs hardware test results from the user.
3. Phase 3: engine plus Strobe first, then other effects, with measured timing.
4. Phase 4: WPF app.
5. Phase 5: sleep/resume, shutdown, installer, troubleshooting docs.
