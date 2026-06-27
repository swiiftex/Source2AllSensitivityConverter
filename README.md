# Source → All Sensitivity Converter

A Windows desktop app (.NET 9 / WPF) that takes a mouse sensitivity **from any supported game** (or a
raw **cm/360 + DPI**) and converts it into the equivalent in-game sensitivity for every game it finds
installed on your machine — scanning Steam, Epic and GOG libraries, detecting each game's engine, and
(where supported) writing the converted value directly into the game's config.

## What it does

The app has two tabs:

**Convert** — the quick calculator. Pick the game your sensitivity is *from*, type the value, pick the
game to convert *to*, and read the result in a big box with a one-click **Copy** button.

Your last-used input is remembered between runs — both tabs' games/sensitivity, plus the Auto-apply
tab's input mode, **cm/360 and DPI** — so you can reopen and immediately apply to another game. Settings
are stored **portably** in a `UserData` folder next to the executable when that location is writable
(move the app and your settings come with it), otherwise under
`%LOCALAPPDATA%\Source2AllSensitivityConverter`. Settings from an older location are migrated automatically.

**Auto-apply** — convert to every installed game at once:

1. Choose your input — a **game sensitivity** (pick the game + type the value, or **Detect from game**),
   or a raw **cm/360 + DPI**.
2. The app **scans for installed games on startup** (and the **Scan for games** button re-scans on
   demand) — reading each store's configs/registry to find games and their paths. Unmatched installs
   get an engine guess from their on-disk file structure.
3. **Check the games** you want and click **Apply to selected** — the converted value is written to each
   game's config. Every row also shows its value in a **read-only copy box**; games that can't be
   auto-applied are greyed for the checkbox but you can still copy their value and set it in-game.

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

## Supported games

Every supported game, individually. **Status**: ✅ Auto-apply = the app writes the config for you ·
🧮 Convert-only = the app shows the value, you set it in-game · 🚫 No conversion = listed but not
converted. `≈` marks a commonly-cited (approximate) conversion factor.

| Game | Engine | Status |
|------|--------|--------|
| Counter-Strike 2 | Source 2 | ✅ Auto-apply |
| Deadlock | Source 2 | ✅ Auto-apply |
| Team Fortress 2 | Source | ✅ Auto-apply |
| Counter-Strike: Source | Source | ✅ Auto-apply |
| Half-Life 2 | Source | ✅ Auto-apply |
| Half-Life: Source | Source | ✅ Auto-apply |
| Half-Life 2: Deathmatch | Source | ✅ Auto-apply |
| Left 4 Dead 2 | Source | ✅ Auto-apply |
| Portal | Source | ✅ Auto-apply |
| Portal 2 | Source | ✅ Auto-apply |
| Garry's Mod | Source | ✅ Auto-apply |
| Day of Defeat: Source | Source | ✅ Auto-apply |
| Black Mesa | Source | ✅ Auto-apply |
| Insurgency | Source | ✅ Auto-apply |
| Day of Infamy | Source | ✅ Auto-apply |
| Apex Legends | Source | ✅ Auto-apply |
| Titanfall 2 | Source | ✅ Auto-apply |
| Half-Life | GoldSrc | ✅ Auto-apply |
| Counter-Strike 1.6 | GoldSrc | ✅ Auto-apply |
| Counter-Strike: Condition Zero | GoldSrc | ✅ Auto-apply |
| Day of Defeat | GoldSrc | ✅ Auto-apply |
| Team Fortress Classic | GoldSrc | ✅ Auto-apply |
| Half-Life: Opposing Force | GoldSrc | ✅ Auto-apply |
| Sven Co-op | GoldSrc | ✅ Auto-apply |
| Quake Champions | id Tech | ✅ Auto-apply |
| DOOM Eternal | id Tech | ✅ Auto-apply |
| Call of Duty 2 | Call of Duty (IW) | ✅ Auto-apply |
| Call of Duty 4: Modern Warfare | Call of Duty (IW) | ✅ Auto-apply |
| Call of Duty: World at War | Call of Duty (IW) | ✅ Auto-apply |
| Call of Duty: Modern Warfare 2 (2009) | Call of Duty (IW) | ✅ Auto-apply |
| Call of Duty: Modern Warfare 3 (2011) | Call of Duty (IW) | ✅ Auto-apply |
| Call of Duty: Black Ops III | Call of Duty (IW) | ✅ Auto-apply |
| Call of Duty: Black Ops II | Call of Duty (IW) | 🧮 Convert-only |
| Call of Duty: Modern Warfare (2019) | Call of Duty (IW) | 🧮 Convert-only \* |
| Call of Duty: Warzone | Call of Duty (IW) | 🧮 Convert-only \* |
| Call of Duty: Black Ops Cold War | Call of Duty (IW) | 🧮 Convert-only \* |
| Call of Duty: Vanguard | Call of Duty (IW) | 🧮 Convert-only \* |
| Call of Duty: Modern Warfare II (2022) | Call of Duty (IW) | 🧮 Convert-only \* |
| Call of Duty: Modern Warfare III (2023) | Call of Duty (IW) | 🧮 Convert-only \* |
| Call of Duty: Black Ops 6 | Call of Duty (IW) | 🧮 Convert-only \* |
| Call of Duty: Black Ops 7 | Call of Duty (IW) | 🧮 Convert-only \* |
| Rainbow Six Siege | Unreal Engine | ✅ Auto-apply |
| Mordhau | Unreal Engine | ✅ Auto-apply |
| The Finals | Unreal Engine | ✅ Auto-apply |
| Fortnite | Unreal Engine | 🧮 Convert-only |
| Marvel Rivals | Unreal Engine | 🧮 Convert-only ≈ |
| Halo Infinite | Unreal Engine | 🧮 Convert-only ≈ |
| Chivalry 2 | Unreal Engine | 🧮 Convert-only |
| Chivalry: Medieval Warfare | Unreal Engine | 🧮 Convert-only |
| Ready or Not | Unreal Engine | 🧮 Convert-only |
| ARC Raiders | Unreal Engine | 🧮 Convert-only |
| Rising Storm 2: Vietnam | Unreal Engine | 🧮 Convert-only |
| PUBG: BATTLEGROUNDS | Unreal Engine | 🚫 No conversion |
| Rust | Unity | ✅ Auto-apply |
| BattleBit Remastered | Unity | 🧮 Convert-only ≈ |
| VALORANT | Riot | 🧮 Convert-only |
| Overwatch 2 | Overwatch | 🧮 Convert-only |
| Minecraft (Java) | Other | ✅ Auto-apply |

\* The modern Call of Duty titles all share one **“Call of Duty (Modern Warfare 2019 and newer)”**
entry in the app's dropdown (same engine, same conversion) — they're listed separately here for clarity.

A few convert-only cases, for reference: **Overwatch 2** has no sensitivity key in its local config
(it's account/cloud-stored — verified); **Fortnite**'s `ClientSettings.Sav` is a plain (un-encrypted)
UE float, but it re-syncs from the cloud on launch so local edits don't stick; **PUBG**'s sensitivity
is FOV/scope-dependent with no agreed single yaw, so it's listed without a conversion.

### How auto-apply works

Each game carries its own **yaw constant**, so the converted value is correct for that engine — not just
the file write. A one-time `*.s2a-backup` copy is made before any config is edited, so changes are
reversible. Rainbow Six is special-cased: its sensitivity is `MouseYawSensitivity × (MultiplierUnit/0.02)`,
so the writer pins Yaw/Pitch to `50` and carries the exact value in the float `MouseSensitivityMultiplierUnit`.

Some games (e.g. The Finals) store settings in a binary Unreal **GVAS `.sav`** as a `Map<Name,Str>`;
the writer edits the value string in place and adjusts the map's payload-size field so the save stays
valid. Writers are deliberately conservative: external configs/saves are **only edited if they already
exist** (launch the game once first), the value is replaced in place when present, and nothing is ever
fabricated in a guessed location. Source/GoldSrc `autoexec.cfg` is the exception — it's created if missing because it runs
on launch and overrides the managed config. Minecraft (which no store launcher reports) is detected
directly from `%APPDATA%/.minecraft`.

VALORANT, Overwatch 2 and Call of Duty are convert-only because their settings are cloud-synced, opaquely
scaled, or keyed by numeric IDs; their value still appears in the copyable output box.

## Adding your own game

Two ways in:
- **Manually add game** (toolbar) — add a brand-new game.
- **Add config…** (Details panel) — select an *unsupported* game in the list and add a config for it.
  This **upgrades that row in place** (showing its conversion immediately) instead of creating a
  duplicate; the name is pre-filled for you.

In the editor:

1. **Game name** + the value in that game that feels like CS2/Source sensitivity 1.0 (e.g. if CS2 1.0
   matches `0.15`, enter `0.15`). That alone makes it convertible.
2. Tick **Enable auto-apply** to reveal the config settings.
3. **Browse** to the save/config file — plain text (`.cfg`/`.ini`/`.txt`) **or** a binary Unreal
   `.sav`. Text files are shown as-is; `.sav` files are parsed and shown as readable `key = value` lines.
   When the file opens, the app jumps to and highlights the sensitivity value automatically.
4. **Highlight the sensitivity number** and click **Detect from highlight**. The app works out the type
   — for `.sav` it reads the GVAS storage type (`text` / `integer` / `float` / `double`); for text it
   tells integer from decimal — and asks you to confirm
   *"The detected sensitivity is 0.50 (float value) — is this correct?"*
   - **Repeat for each value** if the config has several sensitivities (hipfire, ADS, scope…). Each
     confirmed value is added to a write list shown under the buttons; **Clear** empties it.
5. **Save**.

On apply, the app writes **every** confirmed value (the same converted number) to the file: plain text
via `{value}` line templates, GVAS strings via the in-place string writer (adjusting the map size), and
GVAS numerics by overwriting the raw int/float/double. A `*.s2a-backup` is taken first, and the file is
only touched if it exists. Added games are saved to the same portable `settings.json` and reappear
(merged onto the matching installed game, never duplicated) on every launch.

## Build & run

```powershell
dotnet build
dotnet run
```

Requires the .NET 9 SDK on Windows. The app only reads store configs and writes the config files of
games you explicitly select.

## Releases (CI)

`.github/workflows/release.yml` runs on every push to `main`: it publishes a **self-contained,
single-file** `win-x64` build (no .NET install needed to run it) and attaches the `.exe` and a zip
to a GitHub Release tagged `v1.0.<run-number>`. No manual steps — push to `main` and the packaged
app appears under **Releases**.

## Ideas for future ease-of-use

Candidate features, roughly in order of value-to-effort:

- **DPI field on the Convert tab** + live cm/360 readout, and a "lock cm/360" mode that holds the
  physical distance constant while you switch games.
- **Favourites / pinned games** and a search box on the dropdowns (the list is getting long).
- **System tray + global hotkey** to pop the converter over a running game.
- **Per-game ADS / scope sensitivity** (zoom multipliers, FOV-aware conversions for PUBG/Apex/CoD).
- **Backup manager** — list and one-click *restore* the `*.s2a-backup` files the app creates.
- **Auto-detect the source game** by reading the most recently modified config among installed games.
- **Profiles** — save/load named setups (e.g. "casual 40 cm/360" vs "competitive 28 cm/360").
- **Light theme toggle** and remembered window size/position.
- **More stores** — EA app, Battle.net, Riot, Xbox/Game Pass scanning.
- **Export/import settings** for moving between PCs.

## Project layout

```
Models/        Game/engine data models, enums, engine display names
Services/      SensitivityConverter, GameCatalog, EngineDetector, InstalledGameScanner, SettingsStore
  Scanners/    Steam / Epic / GOG / Minecraft scanners
  Appliers/    Config writers (Source autoexec, cvar, INI, R6, Unreal axis, Minecraft)
  Vdf/         Valve KeyValues (.vdf/.acf) parser
ViewModels/    MVVM layer (MainViewModel, GameRowViewModel, SourceOption)
Themes/        DarkTheme.xaml (app-wide styles)
MainWindow.*   WPF UI (Convert + Auto-apply tabs)
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
