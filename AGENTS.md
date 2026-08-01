# AGENTS.md

CounterStrikeSharp plugin for CS2 ("One-Man-Army PvE": 1 human vs 10 bots). Two independent .NET 10 projects, no solution file, no tests.

## Projects

- `OneManArmyPve/` — the plugin (class library, `net10.0`). Single source file `OneManArmyPve.cs`. Sole dependency: `CounterStrikeSharp.API` **1.0.371** (pinned; must match the server's CSS version).
- `DeployTool/` — Windows Forms GUI (`WinExe`, `net10.0-windows`) that copies the built DLL into the CS2 plugins folder and launches CS2 with `-insecure`.

## Build

No `.sln` exists — build each project directly:

```powershell
dotnet build OneManArmyPve/OneManArmyPve.csproj
dotnet build DeployTool/DeployTool.csproj
```

No tests, linters, or formatters are configured. `dotnet build` is the only verification step. Both projects target **.NET 10** (`net10.0` / `net10.0-windows`); SDK 10.0.302+ is required.

## Deploy

`DeployTool` is **not** a project reference to the plugin. After building both, manually copy the plugin DLL next to the deploy tool exe before running the GUI:

```
OneManArmyPve/bin/Debug/net10.0/OneManArmyPve.dll
  -> DeployTool/bin/Debug/net10.0-windows/OneManArmyPve.dll
```

The GUI then installs to `<CS2>\game\csgo\addons\counterstrikesharp\plugins\OneManArmyPve\`. CS2 must run with `-insecure` for CounterStrikeSharp plugins to load (the tool passes this automatically).

## Plugin notes

- Console commands `oma_enable [t|ct]`, `oma_disable`, `oma_status` are the plugin's public entrypoints (`OneManArmyPve.cs`).
- `Load(bool hotReload)` supports hot reload.
- Health/armor is applied via `Utilities.SetStateChanged` with hardcoded CS2 schema names (`CBaseEntity`/`m_iHealth`, `CCSPlayerPawn`/`m_ArmorValue`) that can break across game updates. The empty `catch { }` blocks around pawn/item access are **intentional** (keep the plugin running across schema/runtime mismatches), not bugs to fix.
