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
  - Full error reporting catching typed backend exceptions.

### Next
- Phase 3: Effect engine and stepped lighting patterns (Strobe, Breathing, Wave).
