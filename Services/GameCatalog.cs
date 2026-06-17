using Source2AllSensitivityConverter.Models;
using Source2AllSensitivityConverter.Services.Appliers;

namespace Source2AllSensitivityConverter.Services;

/// <summary>
/// The curated database of known games: how to recognise each install, its sensitivity model
/// (yaw constant), and how to write the converted value when supported.
///
/// Yaw constants are the degrees-per-count at sensitivity 1.0. The well-established ones are
/// exact (Source 0.022, Valorant 0.07, Overwatch 0.0066); others are marked approximate.
/// A null yaw means we have no reliable linear conversion for that title.
/// </summary>
public static class GameCatalog
{
    public static IReadOnlyList<GameDefinition> All { get; } = Build();

    private static List<GameDefinition> Build()
    {
        const double srcYaw = SensitivityConverter.SourceYaw; // 0.022

        return
        [
            // ---- Source / Source 2 (linear yaw 0.022, auto-applicable via autoexec.cfg) ----
            new GameDefinition
            {
                Name = "Counter-Strike 2", Engine = Engine.Source2, YawConstant = srcYaw,
                SteamAppId = 730, InstallDirHints = ["counter-strike global offensive"],
                MarkerFiles = ["game/csgo/gameinfo.gi", "game/bin/win64/engine2.dll"],
                Applier = new SourceCfgApplier("game/csgo"),
            },
            new GameDefinition
            {
                Name = "Team Fortress 2", Engine = Engine.Source, YawConstant = srcYaw,
                SteamAppId = 440, InstallDirHints = ["team fortress 2"],
                MarkerFiles = ["tf/gameinfo.txt", "hl2.exe"],
                Applier = new SourceCfgApplier("tf"),
            },
            new GameDefinition
            {
                Name = "Counter-Strike: Source", Engine = Engine.Source, YawConstant = srcYaw,
                SteamAppId = 240, InstallDirHints = ["counter-strike source"],
                MarkerFiles = ["cstrike/gameinfo.txt", "hl2.exe"],
                Applier = new SourceCfgApplier("cstrike"),
            },
            new GameDefinition
            {
                Name = "Half-Life 2", Engine = Engine.Source, YawConstant = srcYaw,
                SteamAppId = 220, InstallDirHints = ["half-life 2"],
                MarkerFiles = ["hl2/gameinfo.txt", "hl2.exe"],
                Applier = new SourceCfgApplier("hl2"),
            },
            new GameDefinition
            {
                Name = "Left 4 Dead 2", Engine = Engine.Source, YawConstant = srcYaw,
                SteamAppId = 550, InstallDirHints = ["left 4 dead 2"],
                MarkerFiles = ["left4dead2/gameinfo.txt", "left4dead2.exe"],
                Applier = new SourceCfgApplier("left4dead2"),
            },
            new GameDefinition
            {
                Name = "Portal 2", Engine = Engine.Source, YawConstant = srcYaw,
                SteamAppId = 620, InstallDirHints = ["portal 2"],
                MarkerFiles = ["portal2/gameinfo.txt", "portal2.exe"],
                Applier = new SourceCfgApplier("portal2"),
            },
            new GameDefinition
            {
                Name = "Garry's Mod", Engine = Engine.Source, YawConstant = srcYaw,
                SteamAppId = 4000, InstallDirHints = ["garrysmod"],
                MarkerFiles = ["garrysmod/gameinfo.txt", "gmod.exe"],
                Applier = new SourceCfgApplier("garrysmod"),
            },
            new GameDefinition
            {
                Name = "Day of Defeat: Source", Engine = Engine.Source, YawConstant = srcYaw,
                SteamAppId = 300, InstallDirHints = ["day of defeat source"],
                MarkerFiles = ["dod/gameinfo.txt", "hl2.exe"],
                Applier = new SourceCfgApplier("dod"),
            },

            // ---- GoldSrc (Half-Life 1 engine): autoexec.cfg in the mod root, yaw 0.022 ----
            new GameDefinition
            {
                Name = "Half-Life", Engine = Engine.GoldSrc, YawConstant = srcYaw,
                SteamAppId = 70, InstallDirHints = ["half-life"],
                MarkerFiles = ["hl.exe", "valve/liblist.gam"],
                Applier = new SourceCfgApplier("valve", cfgSubdir: ""),
            },
            new GameDefinition
            {
                Name = "Counter-Strike 1.6", Engine = Engine.GoldSrc, YawConstant = srcYaw,
                SteamAppId = 10, InstallDirHints = ["half-life", "counter-strike"],
                MarkerFiles = ["hl.exe", "cstrike/liblist.gam"],
                Applier = new SourceCfgApplier("cstrike", cfgSubdir: ""),
            },
            new GameDefinition
            {
                Name = "Day of Defeat", Engine = Engine.GoldSrc, YawConstant = srcYaw,
                SteamAppId = 30, InstallDirHints = ["half-life", "day of defeat"],
                MarkerFiles = ["hl.exe", "dod/liblist.gam"],
                Applier = new SourceCfgApplier("dod", cfgSubdir: ""),
            },
            new GameDefinition
            {
                Name = "Team Fortress Classic", Engine = Engine.GoldSrc, YawConstant = srcYaw,
                SteamAppId = 20, InstallDirHints = ["half-life", "team fortress classic"],
                MarkerFiles = ["hl.exe", "tfc/liblist.gam"],
                Applier = new SourceCfgApplier("tfc", cfgSubdir: ""),
            },
            new GameDefinition
            {
                Name = "Half-Life: Opposing Force", Engine = Engine.GoldSrc, YawConstant = srcYaw,
                SteamAppId = 50, InstallDirHints = ["half-life", "opposing force"],
                MarkerFiles = ["hl.exe", "gearbox/liblist.gam"],
                Applier = new SourceCfgApplier("gearbox", cfgSubdir: ""),
            },

            // ---- Source forks (Respawn): cvar config under Saved Games / Documents ----
            new GameDefinition
            {
                Name = "Apex Legends", Engine = Engine.Source, YawConstant = srcYaw,
                SteamAppId = 1172470, InstallDirHints = ["apex legends"],
                MarkerFiles = ["r5apex.exe"],
                Applier = new CvarApplier(
                    "Saved Games/Respawn/Apex/local/settings.cfg  (mouse_sensitivity)",
                    UserConfigPaths.Apex, "mouse_sensitivity"),
            },
            new GameDefinition
            {
                Name = "Titanfall 2", Engine = Engine.Source, YawConstant = srcYaw,
                SteamAppId = 1237970, InstallDirHints = ["titanfall2", "titanfall 2"],
                MarkerFiles = ["Titanfall2.exe"],
                Applier = new CvarApplier(
                    "Documents/Respawn/Titanfall2/local/settings.cfg  (mouse_sensitivity)",
                    UserConfigPaths.Titanfall2, "mouse_sensitivity"),
            },

            // ---- Other linear engines: convertible, not auto-applied ----
            new GameDefinition
            {
                Name = "VALORANT", Engine = Engine.Riot, YawConstant = 0.07,
                InstallDirHints = ["valorant"], MarkerFiles = ["VALORANT.exe"],
                Applier = null,
            },
            new GameDefinition
            {
                Name = "Overwatch 2", Engine = Engine.Overwatch, YawConstant = 0.0066,
                SteamAppId = 2357570, InstallDirHints = ["overwatch"], MarkerFiles = ["Overwatch.exe"],
                Applier = null,
            },
            new GameDefinition
            {
                Name = "Quake Champions", Engine = Engine.IdTech, YawConstant = srcYaw,
                SteamAppId = 611500, InstallDirHints = ["quake champions"],
                MarkerFiles = ["QuakeChampions.exe"],
                Applier = new CvarApplier(
                    "AppData/Local/id Software/Quake Champions/client/config/input.cfg  (sensitivity)",
                    UserConfigPaths.QuakeChampions, "sensitivity", setaStyle: true),
            },
            new GameDefinition
            {
                Name = "DOOM Eternal", Engine = Engine.IdTech, YawConstant = srcYaw,
                SteamAppId = 782330, InstallDirHints = ["doometernal", "doom eternal"],
                MarkerFiles = ["DOOMEternalx64vk.exe"],
                Applier = new CvarApplier(
                    "Saved Games/id Software/DOOMEternal/base/DOOMEternalConfig.local  (sensitivity)",
                    UserConfigPaths.DoomEternal, "sensitivity", setaStyle: true),
            },
            new GameDefinition
            {
                Name = "Call of Duty: Modern Warfare", Engine = Engine.CallOfDuty,
                YawConstant = 0.0066 /* approximate */,
                SteamAppId = 1938090,
                InstallDirHints = ["call of duty modern warfare"],
                MarkerFiles = ["ModernWarfare.exe"],
                Applier = null,
            },

            // ---- Known engine, no reliable linear conversion (listed, greyed, no output) ----
            new GameDefinition
            {
                Name = "Fortnite", Engine = Engine.Unreal, YawConstant = null,
                InstallDirHints = ["fortnite"],
                MarkerFiles = ["FortniteGame/Binaries/Win64/FortniteClient-Win64-Shipping.exe"],
                Applier = null,
            },
            new GameDefinition
            {
                Name = "PUBG: BATTLEGROUNDS", Engine = Engine.Unreal, YawConstant = null,
                SteamAppId = 578080, InstallDirHints = ["pubg"],
                MarkerFiles = ["TslGame/Binaries/Win64/TslGame.exe"],
                Applier = null,
            },
            new GameDefinition
            {
                Name = "Rust", Engine = Engine.Unity, YawConstant = null,
                SteamAppId = 252490, InstallDirHints = ["rust"],
                MarkerFiles = ["RustClient.exe", "Rust_Data"],
                Applier = null,
            },
        ];
    }

    public static GameDefinition? MatchByAppId(int appId)
        => All.FirstOrDefault(g => g.SteamAppId == appId);

    public static GameDefinition? MatchByName(string installDirOrName)
    {
        var s = installDirOrName.ToLowerInvariant();
        return All.FirstOrDefault(g => g.InstallDirHints.Any(h => s.Contains(h)));
    }
}
