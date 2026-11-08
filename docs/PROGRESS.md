# Progress Log

## 2026-11-08

- Repo created, scaffold added (.gitignore, docs).
- Recorded verified WMI findings and baseline benchmark.
- Wrote Phase 1 architecture proposal with corrections to the handoff (Off value unverified, no smoothness claims).
- Could not read the two reference repos (fetch errors); revisit.

- Non-elevated WMI test result: `Access denied`. Admin rights required.

- Elevated WMI test: Get/Set work, level 2 read back correctly.

### Phase 2: .NET Console Prototype & Backend (Completed)
- Created solution `LoqLightingStudio.sln` (.NET 8 LTS).
- Added `src/LoqLighting.Core`:
  - `Brightness` enum (`Off`, `Low`, `High`).
  - `LightingState(Brightness Level, byte StateType)` readonly record struct.
  - `ILightingBackend` interface (`ReadAsync`, `WriteAsync`, `SupportedLevels`).
- Added `src/LoqLighting.Backend.Wmi`:
  - `LenovoWmiBackend` implementing `ILightingBackend` over `root\WMI` / `LENOVO_LIGHTING_METHOD`.
  - Bound `Get_Lighting_Current_Status(0)` and `Set_Lighting_Current_Status(0, stateType, level)`.
  - Strict whitelist enforcement: `Low` = 2, `High` = 3 only. `Off` remains disabled for safety.
  - Typed exceptions with actionable troubleshooting guidance:
    - `LenovoWmiAccessDeniedException` (requires elevation).
    - `LenovoWmiClassNotFoundException` (missing ACPI class/driver).
    - `LenovoWmiDisconnectedException` (WMI/RPC service disconnect).
    - `LenovoWmiOperationException` (general WMI/COM faults).
- Added `src/LoqLighting.Probe`:
  - Console CLI tool configured with `app.manifest` (`requireAdministrator`).
  - Implemented commands:
    - `read`: reads current brightness and state type with `[PASS]`.
    - `set low|high`: sets brightness, reads back, and verifies with `[PASS]`/`[FAIL]`. Blocks unverified `set off`.
    - `cycle`: captures initial snapshot, verifies Low, verifies High, and guarantees restoration in `finally` with `[PASS]`/`[FAIL]`.
    - `probe-off`: isolated test for candidate raw 1 (Off) with confirmation prompt, 1500 ms observation delay, read-back verification, and guaranteed `RestoreAsync` in `finally` and on Ctrl+C.
  - Full error reporting catching typed backend exceptions.
- Hardware measurement result: `probe-off` passed on target hardware; raw 1 confirmed as `Off`.

### Phase 3: Animation Engine & Stepped Effects (Completed)
- Backend:
  - Enabled `Off` (raw 1) in `WriteAsync` and `SupportedLevels` (`Off`=1, `Low`=2, `High`=3).
  - Maintained whitelist rejection for any other raw values.
  - Retained `probe-off` for ongoing hardware diagnostic capability.
- Core Animation Engine:
  - Added `Step(Brightness Level, TimeSpan Hold)`.
  - Added `IEffect` interface (`string Name`, `IEnumerable<Step> Generate(EffectSettings)`).
  - Added `EffectSettings` with configurable speed (clamped 0.25x-4.0x), custom `LowLevel`/`HighLevel`, `PauseDuration`, and optional `RunDuration`.
  - Implemented `LightingEngine`:
    - Strict single-run execution: starting a new effect gracefully stops the previous one.
    - Dedicated `CancellationTokenSource` per run.
    - Cancellation awaits animation loop completion prior to restoring state.
    - Redundant write elimination (skips calls if target level matches current).
    - Hardware safety clamp enforcing minimum step hold >= 50 ms.
    - Exponential backoff retry mechanism (up to 3 retries) on transient WMI faults, raising `Faulted` events on persistent failures.
    - Guaranteed snapshot restoration in `finally` block and during Ctrl+C interrupts.
- Unit Test Suite (`tests/LoqLighting.Core.Tests`):
  - Built comprehensive test suite using `FakeBackend`:
    - Verified overlapping runs are prevented.
    - Verified engine stop promptly halts execution (< 250 ms) and restores original brightness.
    - Verified redundant writes are eliminated.
    - Verified backend faults trigger retry exhaustion and halt loop cleanly.
    - Unit tests for all individual effect sequence generators.
- Stepped Effects Implemented:
  - `Strobe`: Alternates High and Low with scaled intervals.
  - `StrobeWithPause`: Alternates High and Low followed by a configurable rest pause.
  - `Breathing`: 4-step stepped illumination cycle (Off -> Low -> High -> Low).
  - `Heartbeat`: Dynamic double-pulse rhythm followed by rest.
  - `Wave`: Multi-step rhythmic sequence across all 3 brightness tiers.
  - `RandomFlash`: Seeded pseudo-random flash intervals.
- CLI Probe Commands:
  - `play <effect> [--speed 1.0] [--seconds 10]`: Real-time effect playback with speed scaling, optional duration limit, and Ctrl+C interrupt handler that cleans up and restores original brightness.
  - `bench [--seconds 30]`: Multi-tier benchmark executing Strobe at 200 ms, 100 ms, and 50 ms step holds. Measures average and maximum WMI invocation latencies, tracks WMI error counts, records process CPU usage (total system & single-core normalized), outputs tabular summary, and guarantees initial state restoration in `finally` and on Ctrl+C.

### Next
- Phase 4: Application layer (WPF UI, system tray icon, profile persistence, and auto-start via Windows Task Scheduler).
