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
            new GameDefinition
            {
                Name = "Deadlock", Engine = Engine.Source2, YawConstant = srcYaw,
                SteamAppId = 1422450, InstallDirHints = ["deadlock"],
                MarkerFiles = ["game/citadel/gameinfo.gi", "game/bin/win64/engine2.dll"],
                Applier = new SourceCfgApplier("game/citadel"),
            },
            new GameDefinition
            {
                Name = "Portal", Engine = Engine.Source, YawConstant = srcYaw,
                SteamAppId = 400, InstallDirHints = ["portal"],
                MarkerFiles = ["portal/gameinfo.txt", "hl2.exe"],
                Applier = new SourceCfgApplier("portal"),
            },
            new GameDefinition
            {
                Name = "Half-Life: Source", Engine = Engine.Source, YawConstant = srcYaw,
                SteamAppId = 280, InstallDirHints = ["half-life 1 source", "half-life source"],
                MarkerFiles = ["hl1/gameinfo.txt", "hl2.exe"],
                Applier = new SourceCfgApplier("hl1"),
            },
            new GameDefinition
            {
                Name = "Half-Life 2: Deathmatch", Engine = Engine.Source, YawConstant = srcYaw,
                SteamAppId = 320, InstallDirHints = ["half-life 2 deathmatch"],
                MarkerFiles = ["hl2mp/gameinfo.txt", "hl2.exe"],
                Applier = new SourceCfgApplier("hl2mp"),
            },
            new GameDefinition
            {
                Name = "Black Mesa", Engine = Engine.Source, YawConstant = srcYaw,
                SteamAppId = 362890, InstallDirHints = ["black mesa"],
                MarkerFiles = ["bms/gameinfo.txt", "bms.exe"],
                Applier = new SourceCfgApplier("bms"),
            },
            new GameDefinition
            {
                Name = "Insurgency", Engine = Engine.Source, YawConstant = srcYaw,
                SteamAppId = 222880, InstallDirHints = ["insurgency2"],
                MarkerFiles = ["insurgency/gameinfo.txt", "insurgency.exe"],
                Applier = new SourceCfgApplier("insurgency"),
            },
            new GameDefinition
            {
                Name = "Day of Infamy", Engine = Engine.Source, YawConstant = srcYaw,
                SteamAppId = 447820, InstallDirHints = ["day of infamy"],
                MarkerFiles = ["doi/gameinfo.txt", "doi.exe"],
                Applier = new SourceCfgApplier("doi"),
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
            new GameDefinition
            {
                Name = "Counter-Strike: Condition Zero", Engine = Engine.GoldSrc, YawConstant = srcYaw,
                SteamAppId = 80, InstallDirHints = ["condition zero", "half-life"],
                MarkerFiles = ["hl.exe", "czero/liblist.gam"],
                Applier = new SourceCfgApplier("czero", cfgSubdir: ""),
            },
            new GameDefinition
            {
                Name = "Sven Co-op", Engine = Engine.GoldSrc, YawConstant = srcYaw,
                SteamAppId = 225840, InstallDirHints = ["sven co-op", "sven coop"],
                MarkerFiles = ["svencoop.exe", "svencoop/liblist.gam"],
                Applier = new SourceCfgApplier("svencoop", cfgSubdir: ""),
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
                // MW2019 and newer ("new" IW/Treyarch engine): CS 1.0 == 3.333333, so yaw = 0.0066.
                Name = "Call of Duty (Modern Warfare 2019 and newer)", Engine = Engine.CallOfDuty,
                YawConstant = srcYaw / 3.333333,
                SteamAppId = 1938090,
                InstallDirHints = ["call of duty modern warfare", "call of duty hq", "call of duty"],
                MarkerFiles = ["ModernWarfare.exe"],
                Applier = null,
            },
            new GameDefinition
            {
                // CoD4 through Advanced Warfare (early IW engine, Quake-derived): CS 1.0 == 1, yaw 0.022.
                Name = "Call of Duty 4: Modern Warfare (early IW engine)", Engine = Engine.CallOfDuty,
                YawConstant = srcYaw / 1.0,
                SteamAppId = 7940, InstallDirHints = ["call of duty 4"],
                MarkerFiles = ["iw3mp.exe", "iw3sp.exe"],
                Applier = null,
            },

            // ---- Modern shooters: convertible for the calculator (cloud/unverified config -> no write) ----
            new GameDefinition
            {
                // Fortnite sensitivity is a percentage; yaw is 0.005555 per 1%. Settings are cloud-synced.
                Name = "Fortnite", Engine = Engine.Unreal, YawConstant = 0.005555,
                InstallDirHints = ["fortnite"],
                MarkerFiles = ["FortniteGame/Binaries/Win64/FortniteClient-Win64-Shipping.exe"],
                Applier = null,
            },
            new GameDefinition
            {
                Name = "Marvel Rivals", Engine = Engine.Unreal, YawConstant = 0.0066, ApproximateYaw = true,
                SteamAppId = 2767030, InstallDirHints = ["marvel rivals", "marvelrivals"],
                MarkerFiles = ["MarvelRivals_Launcher.exe", "MarvelGame"],
                Applier = null,
            },
            new GameDefinition
            {
                // CS 1.0 == 22 in The Finals, so yaw = 0.001 (user-verified). Settings live in a UE
                // GVAS save (Map<Name,Str>) -> written by GvasStringMapApplier.
                Name = "The Finals", Engine = Engine.Unreal, YawConstant = srcYaw / 22.0,
                SteamAppId = 2073850, InstallDirHints = ["the finals", "discovery"],
                MarkerFiles = ["Discovery.exe", "Discovery"],
                Applier = new GvasStringMapApplier(
                    UserConfigPaths.TheFinals, "GameplayOption.Controls.MouseSensitivity"),
            },
            new GameDefinition
            {
                Name = "Halo Infinite", Engine = Engine.Unreal, YawConstant = 0.022, ApproximateYaw = true,
                SteamAppId = 1240440, InstallDirHints = ["halo infinite"],
                MarkerFiles = ["HaloInfinite.exe"],
                Applier = null,
            },
            new GameDefinition
            {
                Name = "BattleBit Remastered", Engine = Engine.Unity, YawConstant = 0.0005, ApproximateYaw = true,
                SteamAppId = 671860, InstallDirHints = ["battlebit"],
                MarkerFiles = ["BattleBit.exe"],
                Applier = null,
            },

            // ---- User-verified yaw constants (CS 1.0 == the value noted) ----
            new GameDefinition
            {
                // CS 1.0 == 0.15. UE4 per-axis AxisConfig in Input.ini -> auto-writable.
                Name = "Mordhau", Engine = Engine.Unreal, YawConstant = srcYaw / 0.15,
                SteamAppId = 629760, InstallDirHints = ["mordhau"],
                MarkerFiles = ["Mordhau.exe", "Mordhau"],
                Applier = new UnrealAxisConfigApplier(
                    "AppData/Local/Mordhau/Saved/Config/WindowsClient/Input.ini  (MouseX/MouseY Sensitivity)",
                    UserConfigPaths.Mordhau),
            },
            new GameDefinition
            {
                // CS 1.0 == 0.010214.
                Name = "Chivalry 2", Engine = Engine.Unreal, YawConstant = srcYaw / 0.010214,
                SteamAppId = 1824220, InstallDirHints = ["chivalry 2", "chivalry2"],
                MarkerFiles = ["TBL/Binaries/Win64/Chivalry2-Win64-Shipping.exe"],
                Applier = null,
            },
            new GameDefinition
            {
                // CS 1.0 == 3.432838.
                Name = "Chivalry: Medieval Warfare", Engine = Engine.Unreal, YawConstant = srcYaw / 3.432838,
                SteamAppId = 219640, InstallDirHints = ["chivalry medieval warfare"],
                MarkerFiles = ["Binaries/Win64/UDK.exe", "Binaries/Win32/UDK.exe"],
                Applier = null,
            },
            new GameDefinition
            {
                // CS 1.0 == 0.125714.
                Name = "Ready or Not", Engine = Engine.Unreal, YawConstant = srcYaw / 0.125714,
                SteamAppId = 1144200, InstallDirHints = ["ready or not", "readyornot"],
                MarkerFiles = ["ReadyOrNot/Binaries/Win64/ReadyOrNot-Win64-Shipping.exe"],
                Applier = null,
            },
            new GameDefinition
            {
                // CS 1.0 == 16.
                Name = "ARC Raiders", Engine = Engine.Unreal, YawConstant = srcYaw / 16.0,
                InstallDirHints = ["arc raiders", "arcraiders"],
                MarkerFiles = ["ARC-Raiders.exe"],
                Applier = null,
            },
            new GameDefinition
            {
                // CS 1.0 == 4.
                Name = "Rising Storm 2: Vietnam", Engine = Engine.Unreal, YawConstant = srcYaw / 4.0,
                SteamAppId = 418460, InstallDirHints = ["rising storm 2"],
                MarkerFiles = ["ROGame.exe", "Binaries/Win64/ROGame.exe"],
                Applier = null,
            },
            new GameDefinition
            {
                // CS 1.0 == 0.10613666 (the options.txt value). Auto-writable text config.
                Name = "Minecraft", Engine = Engine.Other, YawConstant = srcYaw / 0.10613666,
                InstallDirHints = ["minecraft"],
                MarkerFiles = [],
                Applier = new MinecraftOptionsApplier(UserConfigPaths.MinecraftOptions),
            },

            // ---- Known engine, no reliable linear conversion (listed, greyed, no output) ----
            new GameDefinition
            {
                // PUBG's sensitivity is FOV/scope-dependent (LastConvertedMouseSensitivity); public
                // yaw values disagree by ~10x, so we list it but don't risk a wrong conversion.
                Name = "PUBG: BATTLEGROUNDS", Engine = Engine.Unreal, YawConstant = null,
                SteamAppId = 578080, InstallDirHints = ["pubg"],
                MarkerFiles = ["TslGame/Binaries/Win64/TslGame.exe"],
                Applier = null,
            },
            // ---- Other engines with verified configs + yaw constants ----
            new GameDefinition
            {
                // Rust hipfire yaw is 0.1125 deg/count; cvar lives in the install's cfg folder.
                Name = "Rust", Engine = Engine.Unity, YawConstant = 0.1125,
                SteamAppId = 252490, InstallDirHints = ["rust"],
                MarkerFiles = ["RustClient.exe", "Rust_Data"],
                Applier = new CvarApplier(
                    "<install>/cfg/client.cfg  (input.sensitivity)",
                    inst => System.IO.Path.Combine(inst, "cfg", "client.cfg"),
                    "input.sensitivity"),
            },
            new GameDefinition
            {
                // R6 effective yaw is 0.00572958 at MultiplierUnit 0.02; applier keeps that invariant.
                Name = "Rainbow Six Siege", Engine = Engine.Unreal, YawConstant = 0.00572958,
                SteamAppId = 359550, InstallDirHints = ["rainbowsix", "tom clancy's rainbow six siege"],
                MarkerFiles = ["RainbowSix.exe", "RainbowSixGame.exe"],
                Applier = new R6SiegeApplier(UserConfigPaths.RainbowSixSiege),
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
