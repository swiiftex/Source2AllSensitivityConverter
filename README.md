# Source → All Sensitivity Converter

A Windows desktop app (.NET 9 / WPF) that takes a mouse sensitivity **from any supported game** (or a
raw **cm/360 + DPI**) and converts it into the equivalent in-game sensitivity for every game it finds
installed on your machine — scanning Steam, Epic and GOG libraries, detecting each game's engine, and
(where supported) writing the converted value directly into the game's config.

## What it does

1. Choose your input:
   - **Game sensitivity** — pick the game your sensitivity is from in the dropdown (it shows the
     engine next to each name) and type the value, or click **Detect from game** to read it from an
     installed game's config; **or**
   - **cm/360 + DPI** — type the physical distance for a 360° turn and your mouse DPI.
2. **Scan for games** — reads the configs/registry of each store to find installed games and their
   install paths.
3. Each game is matched against a built-in catalog (engine + sensitivity model). For games not in the
   catalog, the engine is guessed from the on-disk file structure.
4. **Check the games** you want and click **Apply to selected** — the converted sensitivity is written
   to each game's config file.
   - Games that can't be auto-applied are **greyed out**. Clicking one still shows its detected engine
     and the computed output sensitivity so you can set it yourself.

## How the conversion works

It uses the **360-distance method**: the physical distance to turn a full 360° is held constant, so the
feel is identical across games at the same mouse DPI. Every input is reduced to a single, engine-neutral
**counts/360** figure, and each target game's sensitivity is derived back from it:

```
counts/360  =  360 / (sensitivity × sourceYaw)        // from a game sensitivity
counts/360  =  cm/360 × DPI / 2.54                     // from a cm/360 + DPI
targetSensitivity  =  360 / (counts/360 × targetYaw)   // for each target game
```

Each game carries a *yaw* constant (degrees per count at sensitivity 1.0). Exact values are used for
the well-established cases (Source `0.022`, Valorant `0.07`, Overwatch `0.0066`, R6 `0.00572958`,
Fortnite `0.005555`, Rust `0.1125`); a few are commonly-cited approximations (Call of Duty, Marvel
Rivals, The Finals, Halo Infinite, BattleBit) and are shown with a `≈` and an "approximate" note.
Games whose input model isn't a simple linear yaw (e.g. **PUBG**, whose value is FOV/scope-dependent)
are listed with their engine but marked *No conversion*.

## Stores & detection

| Store | How installs are found |
|-------|------------------------|
| Steam | `SteamPath` from the registry → `libraryfolders.vdf` → each `appmanifest_*.acf` |
| Epic  | `%ProgramData%\Epic\EpicGamesLauncher\Data\Manifests\*.item` (JSON) |
| GOG   | `HKLM\SOFTWARE\WOW6432Node\GOG.com\Games\*` (registry, 32-bit view) |

Engine detection (for catalog misses) looks for hallmark files: `engine2.dll`/`gameinfo.gi` (Source 2),
`hl2.exe`+`gameinfo.txt` (Source), `*.uproject`/`Engine\Binaries`/`*-Win64-Shipping.exe` (Unreal),
`UnityPlayer.dll`/`*_Data` (Unity), and so on.

## Auto-apply support

A one-time `*.s2a-backup` copy is made before any config file is edited, so changes are reversible.

| Game(s) | Writer | Config target |
|---------|--------|---------------|
| Engine / games | Writer | Config target |
|---|---|---|
| **Source / Source 2** — CS2, TF2, CS:S, HL2, HL:Source, HL2:DM, L4D2, Portal, Portal 2, Garry's Mod, DoD:S, Deadlock, Black Mesa, Insurgency, Day of Infamy | `SourceCfgApplier` | `…/<mod>/cfg/autoexec.cfg` → `sensitivity "x"` |
| **GoldSrc** — Half-Life, CS 1.6, Condition Zero, DoD, TFC, Opposing Force, Sven Co-op | `SourceCfgApplier` (mod root) | `…/<mod>/autoexec.cfg` → `sensitivity "x"` |
| **Apex Legends** | `CvarApplier` | `Saved Games/Respawn/Apex/local/settings.cfg` → `mouse_sensitivity` |
| **Titanfall 2** | `CvarApplier` | `Documents/Respawn/Titanfall2/local/settings.cfg` → `mouse_sensitivity` |
| **Quake Champions** | `CvarApplier` | `…/id Software/Quake Champions/client/config/input.cfg` → `seta sensitivity` |
| **DOOM Eternal** | `CvarApplier` | `Saved Games/id Software/DOOMEternal/base/DOOMEternalConfig.local` → `seta sensitivity` |
| **Rust** (Unity, yaw 0.1125) | `CvarApplier` | `<install>/cfg/client.cfg` → `input.sensitivity` |
| **Rainbow Six Siege** (yaw 0.00572958) | `R6SiegeApplier` | `Documents/My Games/Rainbow Six - Siege/<id>/GameSettings.ini` `[INPUT]` |

Each game carries its own **yaw constant** so the converted value is correct for that engine, not just
the file write. Rainbow Six is special: its sensitivity is `MouseYawSensitivity × (MultiplierUnit/0.02)`,
so the writer pins Yaw/Pitch to `50` and carries the exact value in the float `MouseSensitivityMultiplierUnit`.

Source/GoldSrc autoexec is created if missing (it runs on launch, overriding the managed config). The
external writers are deliberately conservative: they **only edit a config file that already exists**
(launch the game once first), replace the value in place when present, and back it up first
(`*.s2a-backup`) — they never fabricate a file in a guessed location.

Engines whose sensitivity isn't a plain, documented text value are convert-only by default:
**Valorant** (settings are cloud-synced and overwritten on launch), **Overwatch 2** (no confirmable
local key / opaque scaling), and **Call of Duty: MW** (settings keyed by opaque numeric IDs). For
these, enabling experimental apply exports the value instead of risking config corruption.

### "Allow experimental auto-apply" (opt-in)

Tick the **Allow experimental auto-apply** checkbox to un-grey every game that has a *known
conversion* (not just the safely-writable ones). When you then apply:

- Games with a safe writer (Source family) still get their config edited (with backup).
- Games without a verified config format are **not** edited blindly — instead their computed values
  are exported to `Source2All_Sensitivities.txt` on your Desktop and copied to the clipboard, so you
  can paste them in manually. This avoids corrupting configs whose exact key/format isn't confirmed.

As verified writers are added to the catalog (`Applier = …` on a `GameDefinition`), those games will
graduate from "export" to a real in-place edit automatically.

## Build & run

```powershell
dotnet build
dotnet run
```

Requires the .NET 9 SDK on Windows. The app only reads store configs and writes the config files of
games you explicitly select.

## Project layout

```
Models/        Game/engine data models and enums
Services/      SensitivityConverter, GameCatalog, EngineDetector, InstalledGameScanner
  Scanners/    Steam / Epic / GOG store scanners
  Appliers/    Config writers (Source autoexec, generic INI)
  Vdf/         Valve KeyValues (.vdf/.acf) parser
ViewModels/    MVVM layer (MainViewModel, GameRowViewModel)
MainWindow.*   WPF UI
```

## Extending the catalog

Add a `GameDefinition` in `Services/GameCatalog.cs`:

```csharp
new GameDefinition
{
    Name = "My Game", Engine = Engine.Source, YawConstant = 0.022,
    SteamAppId = 12345, InstallDirHints = ["my game"],
    MarkerFiles = ["mygame/gameinfo.txt"],
    Applier = new SourceCfgApplier("mygame"), // or null to make it convert-only
}
```
