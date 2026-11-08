# Benchmarks

## WMI Set latency (baseline, from hardware discovery)

20 alternating Low/High `Set_Lighting_Current_Status` calls, timed with `System.Diagnostics.Stopwatch` from PowerShell.

| Metric | Result |
|---|---:|
| Operations | 20 |
| Average | 8.69 ms |
| Minimum | 7.78 ms |
| Maximum | 14.98 ms |
| Brightness after run | 3 (restored) |

This is **method-call duration only**. It is not LED response time, and 1/8.69 ms (~115 calls/s) is not a usable animation rate.

## Still to measure

- [ ] Sustained switching rate over 60 s (call-duration drift, errors)
- [ ] Visible flicker at various step intervals (50 / 100 / 200 ms)
- [ ] Hardware response time (phone slow-motion capture)
- [ ] CPU usage during continuous animation
- [ ] WMI failures / disconnects during long runs
- [ ] Whether rapid switching gives any perceptual smoothing

Results will be appended here with date, build, and raw numbers.
