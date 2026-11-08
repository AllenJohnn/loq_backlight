# Verified Hardware Findings

Target: Lenovo LOQ 15ARP9, Windows 11, white single-zone backlit keyboard.

## WMI interface (confirmed on the target machine)

| Item | Value |
|---|---|
| Namespace | `root\WMI` |
| Class | `LENOVO_LIGHTING_METHOD` |
| Instance | `ACPI\PNP0C14\GMZN_0` |
| Active | `True` |
| Methods | `Get_Lighting_Current_Status`, `Set_Lighting_Current_Status` |

### Get_Lighting_Current_Status

- In: `Lighting_ID` (UInt8, id 0)
- Out: `Current_Brightness_Level` (UInt8), `Current_State_Type` (UInt8)

### Set_Lighting_Current_Status

| Parameter | WMI ID | Type |
|---|---:|---|
| `Lighting_ID` | 0 | UInt8 |
| `Current_State_Type` | 1 | UInt8 |
| `Current_Brightness_Level` | 2 | UInt8 |

Positional order follows the parameter IDs: `(Lighting_ID, Current_State_Type, Current_Brightness_Level)`.

## Observed behaviour

- Read returned `Current_Brightness_Level = 3`, `Current_State_Type = 0` (keyboard at High).
- Write `(0, 0, 2)` then read back returned `2`; write `(0, 0, 3)` restored `3`.
- Direct brightness control via WMI is confirmed for levels 2 and 3.

## Not yet verified (do not assume)

- The meaning of `Current_State_Type`. Only `0` has been tested.
- Which value means **Off** over WMI. The handoff lists `1 = Off, 2 = Low, 3 = High`, but only 2 and 3 have been observed on Windows. Off must be probed carefully (see `docs/ARCHITECTURE.md`, "Off-value probe").
- Whether administrator rights are required.
- Behaviour after sleep/resume and when Lenovo Vantage / Fn key also change the state.

## Hardware limits

- Three brightness states only. No RGB, no continuous dimming.
- "Smooth" effects are stepped approximations until measured otherwise.
