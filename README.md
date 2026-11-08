# LOQ Lighting Studio

Custom keyboard backlight effects for Lenovo LOQ laptops on Windows 11, built on the Lenovo WMI lighting interface (`root\WMI`, `LENOVO_LIGHTING_METHOD`).

**Status:** Phase 1 (architecture and research). No application code yet.

## Target hardware

- Lenovo LOQ 15ARP9, white backlit keyboard
- Three hardware brightness states only (no RGB, no continuous dimming). Effects are stepped patterns, not fades.

## Planned stack

C# / .NET, WPF UI, `System.Management` behind a hardware-abstraction interface, JSON profiles.

## Docs

- [Architecture](docs/ARCHITECTURE.md)
- [Verified hardware findings](docs/hardware-findings.md)
- [Benchmarks](docs/benchmarks.md)
- [Progress log](docs/PROGRESS.md)

## Roadmap

1. Architecture and research
2. WMI hardware controller prototype (probe CLI)
3. Animation engine and first effects
4. WPF desktop app
5. Reliability, packaging, troubleshooting docs
