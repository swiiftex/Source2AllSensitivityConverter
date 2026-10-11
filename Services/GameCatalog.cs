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

    private const string VcfgSensitivity = @"^\s*""sensitivity""\s+""(?<v>-?[0-9.]+)""";

    // Battlefield: degrees per count = k × (GstInput.MouseSensitivity + 0.005). BF4-family k = 0.04 rad
    // (fits four independent measurements within 0.01%); BF3 k = 0.0092 rad (within 1%).
    private const double Bf4Yaw = 2.2918311805;
    private const double Bf3Yaw = 0.5271211715;
    private const string FrostbiteSensitivity = @"^GstInput\.MouseSensitivity (?<v>-?[0-9.]+)";

    private static GameDefinition Battlefield(string name, int appId, string installDir, double yaw,
        bool approximate, params string[] profileFiles) => new()
    {
        Name = name, Engine = Engine.Frostbite, YawConstant = yaw, SensitivityOffset = 0.005,
        ApproximateYaw = approximate,
        SteamAppId = appId, InstallDirHints = [installDir],
        Applier = profileFiles.Length == 1
            ? new RegexValueApplier($"{profileFiles[0]}  (GstInput.MouseSensitivity)", profileFiles[0], FrostbiteSensitivity, "0.000000")
            : new CompositeApplier(
                profileFiles.Select(f => (IConfigApplier)new RegexValueApplier(
                    $"{f}  (GstInput.MouseSensitivity)", f, FrostbiteSensitivity, "0.000000")).ToList(),
                requireAll: false),
    };

    /// <summary>A Source 1 game: <c>sensitivity</c> written to &lt;install&gt;/&lt;mod&gt;/cfg/autoexec.cfg.</summary>
    private static GameDefinition SourceGame(string name, int appId, string installDir, string modFolder, string exe) => new()
    {
        Name = name, Engine = Engine.Source, YawConstant = SensitivityConverter.SourceYaw,
        SteamAppId = appId, InstallDirHints = [installDir],
        MarkerFiles = [$"{modFolder}/gameinfo.txt", exe],
        Applier = new SourceCfgApplier(modFolder),
    };

    /// <summary>A GoldSrc game in the shared "Half-Life" folder: autoexec.cfg in the mod folder root.</summary>
    private static GameDefinition GoldSrcGame(string name, int appId, string modFolder) => new()
    {
        Name = name, Engine = Engine.GoldSrc, YawConstant = SensitivityConverter.SourceYaw,
        SteamAppId = appId, InstallDirHints = ["half-life"],
        MarkerFiles = ["hl.exe", $"{modFolder}/liblist.gam"],
        Applier = new SourceCfgApplier(modFolder, cfgSubdir: ""),
    };

    /// <summary>
    /// Source 2 games keep the live value in Steam userdata (a local convar file plus its Steam Cloud
    /// copy, for every Steam account on the PC); autoexec.cfg is also written as a fallback, since
    /// it only runs when the game is launched with <c>+exec autoexec</c>.
    /// </summary>
    private static IConfigApplier SourceTwoApplier(string modFolder, int appId, string localVcfg, string remoteVcfg) =>
        new CompositeApplier(
            [
                new RegexValueApplier($"Steam userdata/<account>/{appId}/local/cfg/{localVcfg}  (\"sensitivity\")",
                    $@"{{Steam}}\userdata\*\{appId}\local\cfg\{localVcfg}", VcfgSensitivity, "0.000000"),
                new RegexValueApplier($"Steam userdata/<account>/{appId}/remote/{remoteVcfg}  (\"sensitivity\")",
                    $@"{{Steam}}\userdata\*\{appId}\remote\{remoteVcfg}", VcfgSensitivity, "0.000000"),
                new SourceCfgApplier(modFolder),
            ],
            requireAll: false);

    /// <summary>
    /// Writes the regex group <c>v</c> in every existing file matching any of <paramref name="paths"/>.
    /// Only an existing line is ever changed, so an absent key fails cleanly instead of adding one.
    /// </summary>
    private static IConfigApplier Writer(string regex, string key, string format, params string[] paths)
    {
        static string Describe(string p) => p.Replace("{", "").Replace("}", "").Replace('\\', '/');

        var writers = paths
            .Select(p => (IConfigApplier)new RegexValueApplier($"{Describe(p)}  ({key})", p, regex, format))
            .ToList();
        return writers.Count == 1 ? writers[0] : new CompositeApplier(writers, requireAll: false);
    }

    // Call of Duty from MW2019 on: CS2 1.0 == 3.333333 in game, i.e. yaw 0.0066.
    private const double ModernCodYaw = SensitivityConverter.SourceYaw / 3.333333;
    private const int CodHqAppId = 1938090;

    // MWII/MWIII: MouseHorizontalSensibility:0.0 = "2.75"; BO6 on: MouseHorizontalSensibility@0;12088;6692 = 1.8.
    private const string CodSensibility = @"^MouseHorizontalSensibility(?:[:@][0-9.;]+)?\s*=\s*""?(?<v>-?[0-9.]+)";

    /// <summary>A Call of Duty HQ title: per-account g.*.l.txt files tagged with its codename (both copies).</summary>
    private static IConfigApplier CodHqWriter(string codename) =>
        Writer(CodSensibility, "MouseHorizontalSensibility", "0.000000",
            $@"{{LocalAppData}}\Activision\Call of Duty*\players*\*\g.*{codename}*.l.txt*",
            $@"{{Documents}}\Call of Duty*\players*\*\g.*{codename}*.l.txt*");

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
                Applier = SourceTwoApplier("game/csgo", 730, "cs2_user_convars_0_slot0.vcfg", "cs2_user_convars.vcfg"),
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
                Applier = SourceTwoApplier("game/citadel", 1422450, "user_convars_0_slot0.vcfg", "user_convars.vcfg"),
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
            SourceGame("Left 4 Dead", 500, "left 4 dead", "left4dead", "left4dead.exe"),
            SourceGame("Half-Life Deathmatch: Source", 360, "half-life 1 source deathmatch", "hl1mp", "hl1mp.exe"),
            SourceGame("Counter-Strike: Global Offensive (Legacy)", 4465480, "csgo legacy", "csgo", "csgo.exe"),
            SourceGame("No More Room in Hell", 224260, "nmrih", "nmrih", "nmrih.exe"),
            SourceGame("Fistful of Frags", 265630, "fistful of frags", "fof", "sdk/hl2.exe"),
            SourceGame("Zombie Panic! Source", 17500, "zombie panic source", "zps", "zps.exe"),
            SourceGame("Synergy", 17520, "synergy", "synergy", "synergy.exe"),
            SourceGame("Nuclear Dawn", 17710, "nuclear dawn", "nucleardawn", "nucleardawn.exe"),
            SourceGame("Contagion", 238430, "contagion", "contagion", "contagion.exe"),
            SourceGame("Portal Stories: Mel", 317400, "portal stories mel", "portal_stories", "portal2.exe"),
            SourceGame("Team Fortress 2 Classified", 3545060, "team fortress 2 classified", "tf2classified", "tf2classified_win64.exe"),
            SourceGame("Hunt Down The Freeman", 723390, "hunt down the freeman", "hdtf", "hdtf.exe"),

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
            GoldSrcGame("Half-Life: Blue Shift", 130, "bshift"),
            GoldSrcGame("Ricochet", 60, "ricochet"),
            GoldSrcGame("Deathmatch Classic", 40, "dmc"),

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
                // Sensitivity moved from game.cfg to input.cfg; only an existing line is updated.
                Name = "Quake Champions", Engine = Engine.IdTech, YawConstant = srcYaw,
                SteamAppId = 611500, InstallDirHints = ["quake champions", "quakechampions"],
                MarkerFiles = ["QuakeChampions.exe", "client/bin/pc/QuakeChampions.exe"],
                Applier = Writer(@"^(?:seta\s+)?sensitivity\s+""?(?<v>-?[0-9.]+)", "sensitivity", "0.######",
                    @"{LocalAppData}\id Software\Quake Champions\client\config\input.cfg"),
            },
            new GameDefinition
            {
                // The cvar is m_sensitivity; the game saves it to either .cfg or .local, so both are checked.
                Name = "DOOM Eternal", Engine = Engine.IdTech, YawConstant = srcYaw,
                SteamAppId = 782330, InstallDirHints = ["doometernal", "doom eternal"],
                MarkerFiles = ["DOOMEternalx64vk.exe"],
                Applier = Writer(@"^(?:seta\s+)?m_sensitivity\s+""?(?<v>-?[0-9.]+)", "m_sensitivity", "0.######",
                    @"{SavedGames}\id Software\DOOMEternal\base\DOOMEternalConfig.cfg",
                    @"{SavedGames}\id Software\DOOMEternal\base\DOOMEternalConfig.local"),
            },
            new GameDefinition
            {
                // <install>/<SteamID64>/baseq3/qzconfig.cfg: seta sensitivity "2" (m_yaw 0.022).
                Name = "Quake Live", Engine = Engine.IdTech, YawConstant = srcYaw,
                SteamAppId = 282440, InstallDirHints = ["quake live"],
                MarkerFiles = ["quakelive_steam.exe"],
                Applier = Writer(@"^seta sensitivity ""(?<v>-?[0-9.]+)""", "seta sensitivity", "0.######",
                    @"{Install}\*\baseq3\qzconfig.cfg"),
            },
            // Quake / Quake II remasters and DOOM (2016) are matched by app id only — "quake"/"doom"
            // folder hints would also catch other id games. Their configs are reset by Steam Cloud.
            new GameDefinition
            {
                Name = "Quake (2021)", Engine = Engine.IdTech, YawConstant = srcYaw,
                SteamAppId = 2310, MarkerFiles = ["rerelease/Quake_x64_steam.exe"],
            },
            new GameDefinition
            {
                Name = "Quake II (2023)", Engine = Engine.IdTech, YawConstant = srcYaw, ApproximateYaw = true,
                SteamAppId = 2320, MarkerFiles = ["rerelease/quake2ex_steam.exe"],
            },
            new GameDefinition
            {
                Name = "DOOM (2016)", Engine = Engine.IdTech, YawConstant = srcYaw, ApproximateYaw = true,
                SteamAppId = 379720, MarkerFiles = ["DOOMx64.exe", "DOOMx64vk.exe"],
            },
            new GameDefinition
            {
                // Respawn's first Titanfall; m_sensitivity, but the cfg file is unconfirmed.
                Name = "Titanfall", Engine = Engine.Source, YawConstant = srcYaw, ApproximateYaw = true,
                SteamAppId = 1454890, MarkerFiles = ["Titanfall.exe"],
            },
            new GameDefinition
            {
                // Sensitivity is stored server-side (not in cvars.xml).
                Name = "Destiny 2", Engine = Engine.Other, YawConstant = 0.0066,
                SteamAppId = 1085660, InstallDirHints = ["destiny 2"],
                MarkerFiles = ["destiny2.exe", "destiny2launcher.exe"],
            },
            new GameDefinition
            {
                // Seven indexed MouseSensitivity slots whose meaning is unconfirmed, and the game
                // overwrites hand edits, so this is convert-only.
                Name = "Halo: The Master Chief Collection", Engine = Engine.Unreal, YawConstant = 0.0222222,
                SteamAppId = 976730, InstallDirHints = ["halo the master chief collection"],
                MarkerFiles = ["mcc/binaries/win64/mcc-win64-shipping.exe"],
            },
            new GameDefinition
            {
                // Epic only; 1/60 degree per count at sensitivity 1 (CS2 1.0 == 1.32). Cloud-saved settings.
                Name = "Diabotical", Engine = Engine.Other, YawConstant = 1.0 / 60.0,
                InstallDirHints = ["diabotical"],
            },

            // ---- Call of Duty, MW2019 onward (CS2 1.0 == 3.333333, yaw 0.0066). Every title is its own
            // row — including those inside the shared Call of Duty HQ install — with its own writer. ----
            new GameDefinition
            {
                // Hipfire is the hashed dvar 283349319 in Documents/Call of Duty Modern Warfare/players/config.cfg.
                Name = "Call of Duty: Modern Warfare (2019)", Engine = Engine.CallOfDuty, YawConstant = ModernCodYaw,
                SteamAppId = 2000950, InstallDirHints = ["call of duty modern warfare"],
                MarkerFiles = ["ModernWarfare.exe"],
                Applier = Writer(@"^setcl 283349319 ""(?<v>-?[0-9.]+)""", "setcl 283349319", "0.######",
                    @"{Documents}\Call of Duty Modern Warfare\players\config.cfg"),
            },
            new GameDefinition
            {
                // No sensitivity in any text config (only graphics in player/<id>/config.ini).
                Name = "Call of Duty: Black Ops Cold War", Engine = Engine.CallOfDuty, YawConstant = ModernCodYaw,
                SteamAppId = 1985810, InstallDirHints = ["call of duty black ops cold war"],
                MarkerFiles = ["BlackOpsColdWar.exe"],
            },
            new GameDefinition
            {
                Name = "Call of Duty: Vanguard", Engine = Engine.CallOfDuty, YawConstant = ModernCodYaw,
                SteamAppId = 1985820, InstallDirHints = ["call of duty vanguard"],
                MarkerFiles = ["Vanguard.exe"],
                Applier = Writer(CodSensibility, "MouseHorizontalSensibility", "0.000000",
                    @"{Documents}\Call of Duty Vanguard\players\options*"),
            },
            new GameDefinition
            {
                // settings.<n>.local.cod22.cst per account (verified on disk); "Call of Duty" before July 2025.
                Name = "Call of Duty: Modern Warfare II (2022)", Engine = Engine.CallOfDuty, YawConstant = ModernCodYaw,
                SteamAppId = 3595230, InstallDirHints = ["call of duty modern warfare ii"],
                MarkerFiles = ["cod22-cod.exe"],
                Applier = Writer(CodSensibility, "MouseHorizontalSensibility", "0.000000",
                    @"{Documents}\Call of Duty MWII\players\*\settings.*.local.cod22.cst",
                    @"{Documents}\Call of Duty\players\*\settings.*.local.cod22.cst"),
            },
            new GameDefinition
            {
                Name = "Call of Duty: Modern Warfare III (2023)", Engine = Engine.CallOfDuty, YawConstant = ModernCodYaw,
                SteamAppId = 3595270, InstallDirHints = ["call of duty modern warfare iii"],
                MarkerFiles = ["cod.exe"],
                Applier = Writer(CodSensibility, "MouseHorizontalSensibility", "0.000000",
                    @"{Documents}\Call of Duty MWIII\players\*\*.cst",
                    @"{Documents}\Call of Duty\players\*\gamerprofile*.cst",
                    @"{Documents}\Call of Duty\players\*\*cod23*.cst"),
            },
            new GameDefinition
            {
                // g[.cod24].1.0.l.txt0/txt1 per account; both copies are written.
                Name = "Call of Duty: Black Ops 6", Engine = Engine.CallOfDuty, YawConstant = ModernCodYaw,
                SteamAppId = 4384550, InstallDirHints = ["call of duty black ops 6"],
                HostSteamAppId = CodHqAppId, HostDlcAppIds = [2933620, 2933630],
                MarkerFiles = ["cod-cod24.exe"],
                Applier = Writer(CodSensibility, "MouseHorizontalSensibility", "0.000000",
                    @"{Documents}\Call of Duty*\players\*\g.*cod24*.l.txt*",
                    @"{Documents}\Call of Duty\players\*\g.1.0.l.txt*",
                    @"{LocalAppData}\Activision\Call of Duty*\players\*\g.*cod24*.l.txt*"),
            },
            new GameDefinition
            {
                Name = "Call of Duty: Black Ops 7", Engine = Engine.CallOfDuty, YawConstant = ModernCodYaw,
                HostSteamAppId = CodHqAppId, HostDlcAppIds = [3606480, 3606490, 3606500],
                Applier = CodHqWriter("cod25"),
            },
            new GameDefinition
            {
                // Runs on the current Call of Duty client, so it shares that game's settings files.
                Name = "Call of Duty: Warzone", Engine = Engine.CallOfDuty, YawConstant = ModernCodYaw,
                HostSteamAppId = CodHqAppId, HostDlcAppIds = [1962663, 2573900],
                Applier = CodHqWriter("cod25"),
            },
            new GameDefinition
            {
                // MouseHorizontalSensibility@0;12088;6692 = 1.8 in g.*cod26*.l.txt (verified on the beta).
                Name = "Call of Duty: Modern Warfare 4", Engine = Engine.CallOfDuty, YawConstant = ModernCodYaw,
                HostSteamAppId = CodHqAppId, HostDlcAppIds = [4435490],
                Applier = CodHqWriter("cod26"),
            },
            new GameDefinition
            {
                // Battle.net only (add its folder manually): <install>/players/config.ini.
                Name = "Call of Duty: Black Ops 4", Engine = Engine.CallOfDuty, YawConstant = ModernCodYaw,
                ApproximateYaw = true, InstallDirHints = ["call of duty black ops 4", "black ops 4"],
                MarkerFiles = ["BlackOps4.exe"],
                Applier = Writer(@"^mouse_sensitivity\s*=\s*""(?<v>-?[0-9.]+)""", "mouse_sensitivity", "0.######",
                    @"{Install}\players\config.ini"),
            },
            // ---- Early IW-engine Call of Duty (CS 1.0 == 1, yaw 0.022): players/config[_mp].cfg ----
            new GameDefinition
            {
                // CoD4 (Steam): <install>/players/profiles/<profile>/config_mp.cfg
                Name = "Call of Duty 4: Modern Warfare", Engine = Engine.CallOfDuty, YawConstant = srcYaw,
                SteamAppId = 7940, InstallDirHints = ["call of duty 4"],
                MarkerFiles = ["iw3mp.exe", "iw3sp.exe"], Applier = new CodSensitivityApplier(),
            },
            new GameDefinition
            {
                // WaW: %LOCALAPPDATA%/Activision/CoDWaW/players/profiles/<profile>/config_mp.cfg
                Name = "Call of Duty: World at War", Engine = Engine.CallOfDuty, YawConstant = srcYaw,
                SteamAppId = 10090, InstallDirHints = ["call of duty world at war", "world at war"],
                MarkerFiles = ["CoDWaWmp.exe", "CoDWaW.exe"], Applier = new CodSensitivityApplier("CoDWaW"),
            },
            new GameDefinition
            {
                // CoD2: <install>/players/config.cfg
                Name = "Call of Duty 2", Engine = Engine.CallOfDuty, YawConstant = srcYaw,
                SteamAppId = 2620, InstallDirHints = ["call of duty 2"],
                MarkerFiles = ["CoD2MP_s.exe", "CoD2SP_s.exe"], Applier = new CodSensitivityApplier(),
            },
            new GameDefinition
            {
                // MW2 (2009): <install>/players/config_mp.cfg
                Name = "Call of Duty: Modern Warfare 2 (2009)", Engine = Engine.CallOfDuty, YawConstant = srcYaw,
                SteamAppId = 10180, AdditionalSteamAppIds = [10190],
                InstallDirHints = ["call of duty modern warfare 2", "modern warfare 2"],
                MarkerFiles = ["iw4mp.exe", "iw4sp.exe"], Applier = new CodSensitivityApplier(),
            },
            new GameDefinition
            {
                // MW3 (2011): <install>/players2/config_mp.cfg
                Name = "Call of Duty: Modern Warfare 3 (2011)", Engine = Engine.CallOfDuty, YawConstant = srcYaw,
                SteamAppId = 115300, AdditionalSteamAppIds = [42690],
                InstallDirHints = ["call of duty modern warfare 3", "modern warfare 3"],
                MarkerFiles = ["iw5mp.exe", "iw5sp.exe"], Applier = new CodSensitivityApplier(),
            },
            new GameDefinition
            {
                // Black Ops III (yaw 0.022): plaintext <install>/players/config.ini -> MouseSensitivity = "x".
                Name = "Call of Duty: Black Ops III", Engine = Engine.CallOfDuty, YawConstant = srcYaw,
                SteamAppId = 311210, InstallDirHints = ["call of duty black ops iii", "black ops iii", "black ops 3"],
                MarkerFiles = ["BlackOps3.exe"],
                Applier = new TemplateApplier(
                    inst => System.IO.Path.Combine(inst, "players", "config.ini"),
                    "MouseSensitivity = \"{value}\"", createIfMissing: false),
            },
            new GameDefinition
            {
                // Black Ops (2010): <install>/players/config_mp.cfg.
                Name = "Call of Duty: Black Ops", Engine = Engine.CallOfDuty, YawConstant = srcYaw,
                SteamAppId = 42700, AdditionalSteamAppIds = [42710],
                InstallDirHints = ["call of duty black ops"],
                MarkerFiles = ["BlackOps.exe", "BlackOpsMP.exe"], Applier = new CodSensitivityApplier(),
            },
            new GameDefinition
            {
                // Ghosts: <install>/players2/config[_mp].cfg. Hipfire scales with FOV (exact at default FOV).
                Name = "Call of Duty: Ghosts", Engine = Engine.CallOfDuty, YawConstant = srcYaw, ApproximateYaw = true,
                SteamAppId = 209160, AdditionalSteamAppIds = [209170],
                InstallDirHints = ["call of duty ghosts"],
                MarkerFiles = ["iw6sp64_ship.exe", "iw6mp64_ship.exe"], Applier = new CodSensitivityApplier(),
            },
            new GameDefinition
            {
                // Advanced Warfare: <install>/players2/config[_mp].cfg. FOV-scaled like Ghosts.
                Name = "Call of Duty: Advanced Warfare", Engine = Engine.CallOfDuty, YawConstant = srcYaw, ApproximateYaw = true,
                SteamAppId = 209650, AdditionalSteamAppIds = [209660],
                InstallDirHints = ["call of duty advanced warfare"],
                MarkerFiles = ["s1_sp64_ship.exe", "s1_mp64_ship.exe"], Applier = new CodSensitivityApplier(),
            },
            new GameDefinition
            {
                // MW Remastered: the sensitivity dvar is saved by hash — seta 0xBC3E3CAE "5" in players2.
                Name = "Call of Duty: Modern Warfare Remastered", Engine = Engine.CallOfDuty, YawConstant = srcYaw,
                ApproximateYaw = true,
                SteamAppId = 393080, AdditionalSteamAppIds = [393100],
                InstallDirHints = ["call of duty modern warfare remastered"],
                MarkerFiles = ["h1_sp64_ship.exe", "h1_mp64_ship.exe"],
                Applier = Writer(@"^seta (?i:0xBC3E3CAE) ""(?<v>-?[0-9.]+)""", "seta 0xBC3E3CAE", "0.######",
                    @"{Install}\players2\config.cfg", @"{Install}\players2\config_mp.cfg"),
            },
            new GameDefinition
            {
                // Infinite Warfare: sensitivity is in the binary settings.zip.iw7 -> convert-only.
                Name = "Call of Duty: Infinite Warfare", Engine = Engine.CallOfDuty, YawConstant = srcYaw, ApproximateYaw = true,
                SteamAppId = 292730, InstallDirHints = ["call of duty - infinite warfare", "infinite warfare"],
                MarkerFiles = ["iw7_ship.exe"],
            },
            new GameDefinition
            {
                // WWII: sensitivity is in an unidentified binary file in players2 -> convert-only.
                Name = "Call of Duty: WWII", Engine = Engine.CallOfDuty, YawConstant = srcYaw, ApproximateYaw = true,
                SteamAppId = 476600, AdditionalSteamAppIds = [476620],
                InstallDirHints = ["call of duty wwii"],
                MarkerFiles = ["s2_sp64_ship.exe", "s2_mp64_ship.exe"],
            },
            new GameDefinition
            {
                // Black Ops II (yaw 0.022): settings are in binary .cgp profiles -> convert-only.
                Name = "Call of Duty: Black Ops II", Engine = Engine.CallOfDuty, YawConstant = srcYaw,
                SteamAppId = 202970, AdditionalSteamAppIds = [202990, 212910],
                InstallDirHints = ["call of duty black ops ii", "black ops ii", "black ops 2"],
                MarkerFiles = ["t6mp.exe", "t6sp.exe", "t6zm.exe"],
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
                // CS 1.0 == 0.010214. GameUserSettings.ini: MouseSensitivity=0.035000 (verified on disk).
                Name = "Chivalry 2", Engine = Engine.Unreal, YawConstant = srcYaw / 0.010214,
                SteamAppId = 1824220, InstallDirHints = ["chivalry 2", "chivalry2"],
                MarkerFiles = ["TBL/Binaries/Win64/Chivalry2-Win64-Shipping.exe"],
                Applier = new RegexValueApplier(
                    "LocalAppData/Chivalry 2/Saved/Config/WindowsNoEditor/GameUserSettings.ini  (MouseSensitivity)",
                    @"{LocalAppData}\Chivalry 2\Saved\Config\WindowsNoEditor\GameUserSettings.ini",
                    @"^MouseSensitivity=(?<v>-?[0-9.]+)", "0.000000"),
            },
            new GameDefinition
            {
                // CS 1.0 == 3.432838. UDKInput.ini [Engine.PlayerInput]: MouseSensitivity=9.580148 (verified).
                Name = "Chivalry: Medieval Warfare", Engine = Engine.Unreal, YawConstant = srcYaw / 3.432838,
                SteamAppId = 219640, InstallDirHints = ["chivalry medieval warfare", "chivalrymedievalwarfare"],
                MarkerFiles = ["Binaries/Win64/UDK.exe", "Binaries/Win32/UDK.exe"],
                Applier = new RegexValueApplier(
                    "Documents/My Games/Chivalry Medieval Warfare/UDKGame/Config/UDKInput.ini  (MouseSensitivity)",
                    @"{Documents}\My Games\Chivalry Medieval Warfare\UDKGame\Config\UDKInput.ini",
                    @"^MouseSensitivity=(?<v>-?[0-9.]+)", "0.000000"),
            },
            new GameDefinition
            {
                // CS 1.0 == 0.125714 (config scale = slider scale). Windows\ or WinGDK\ (Microsoft Store).
                Name = "Ready or Not", Engine = Engine.Unreal, YawConstant = srcYaw / 0.125714,
                SteamAppId = 1144200, InstallDirHints = ["ready or not", "readyornot"],
                MarkerFiles = ["ReadyOrNot/Binaries/Win64/ReadyOrNot-Win64-Shipping.exe"],
                Applier = Writer(@"^MouseSensitivity=(?<v>-?[0-9.]+)", "MouseSensitivity", "0.000000",
                    @"{LocalAppData}\ReadyOrNot\Saved\Config\Win*\GameUserSettings.ini"),
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
                // CS 1.0 == 4 (config scale = slider scale). ROInput.ini [Engine.PlayerInput].
                Name = "Rising Storm 2: Vietnam", Engine = Engine.Unreal, YawConstant = srcYaw / 4.0,
                SteamAppId = 418460, InstallDirHints = ["rising storm 2"],
                MarkerFiles = ["ROGame.exe", "Binaries/Win64/ROGame.exe", "Binaries/Win64/RisingStorm2.exe"],
                Applier = Writer(@"^MouseSensitivity=(?<v>-?[0-9.]+)", "MouseSensitivity", "0.000000",
                    @"{Documents}\My Games\Rising Storm 2\ROGame\Config\ROInput.ini"),
            },
            new GameDefinition
            {
                // Control.ini is JSON despite the name: "MouseSensitivity": 0.33 (yaw 0.125, CS 1.0 == 0.176).
                Name = "Escape from Tarkov", Engine = Engine.Unity, YawConstant = 0.125,
                SteamAppId = 3932890, InstallDirHints = ["escape from tarkov", "escapefromtarkov"],
                MarkerFiles = ["EscapeFromTarkov.exe"],
                Applier = Writer(@"^\s*""MouseSensitivity"":\s*(?<v>-?[0-9.]+)", "MouseSensitivity", "0.######",
                    @"{AppData}\Battlestate Games\Escape from Tarkov\Settings\Control.ini"),
            },
            new GameDefinition
            {
                // Controls.json: "mouseSensitivity": 1.53. Yaw 0.14 (KovaaK's list); the folder is Steam Cloud synced.
                Name = "Insurgency: Sandstorm", Engine = Engine.Unreal, YawConstant = 0.14, ApproximateYaw = true,
                SteamAppId = 581320, InstallDirHints = ["sandstorm"],
                MarkerFiles = ["Insurgency.exe", "Insurgency/Binaries/Win64/InsurgencyClient-Win64-Shipping.exe"],
                Applier = Writer(@"""mouseSensitivity"":\s*(?<v>-?[0-9.]+)", "mouseSensitivity", "0.######",
                    @"{LocalAppData}\Insurgency\Saved\SaveGames\Profile\Controls.json"),
            },
            new GameDefinition
            {
                // <install>/user/profiles/default/attributes.xml: <Attr name="MouseSensitivity" value="0.22"/>.
                // Yaw 0.0429718 (0.00075 rad) is from converter tables, not measured here.
                Name = "Hunt: Showdown 1896", Engine = Engine.CryEngine, YawConstant = 0.0429718, ApproximateYaw = true,
                SteamAppId = 594650, InstallDirHints = ["hunt showdown"],
                MarkerFiles = ["hunt.exe"],
                Applier = Writer(@"<Attr name=""MouseSensitivity"" value=""(?<v>-?[0-9.]+)""", "MouseSensitivity", "0.######",
                    @"{Install}\user\profiles\default\attributes.xml"),
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

            // ---- Battlefield (Frostbite): turn rate = k × (value + 0.005), fitted to measured data ----
            Battlefield("Battlefield 4", 1238860, "battlefield 4", Bf4Yaw, approximate: false,
                @"{Documents}\Battlefield 4\settings\PROFSAVE_profile"),
            Battlefield("Battlefield 2042", 1517290, "battlefield 2042", Bf4Yaw, approximate: false,
                @"{Documents}\Battlefield 2042\settings\PROFSAVE_profile"),
            Battlefield("Battlefield 1", 1238840, "battlefield 1", Bf4Yaw, approximate: true,
                @"{Documents}\Battlefield 1\settings\PROFSAVE_profile",
                @"{Documents}\Battlefield 1\settings\PROFSAVE_profile_synced"),
            Battlefield("Battlefield V", 1238810, "battlefield v", Bf4Yaw, approximate: true,
                @"{Documents}\Battlefield V\settings\PROFSAVE_profile",
                @"{Documents}\Battlefield V\settings\PROFSAVE_profile_synced"),
            Battlefield("Battlefield 3", 1238820, "battlefield 3", Bf3Yaw, approximate: true,
                @"{Documents}\Battlefield 3\settings\PROF_SAVE_profile"),
            new GameDefinition
            {
                // Config verified on disk (settings\<platform>\PROFSAVEbf6mp_profile, GstInput.MouseSensitivity),
                // but published measurements of its turn rate disagree by ~30% — no conversion until measured.
                Name = "Battlefield 6", Engine = Engine.Frostbite, YawConstant = null,
                SteamAppId = 2807960, InstallDirHints = ["battlefield 6", "battlefield6"],
                MarkerFiles = ["bf6.exe", "Battlefield6.exe"],
                Applier = null,
            },
        ];
    }

    public static GameDefinition? MatchByAppId(int appId)
        => All.FirstOrDefault(g => g.SteamAppId == appId || g.AdditionalSteamAppIds.Contains(appId));

    /// <summary>Games installed inside a launcher app (e.g. the titles in Call of Duty HQ).</summary>
    public static IReadOnlyList<GameDefinition> HostedBy(int appId)
        => All.Where(g => g.HostSteamAppId == appId).ToList();

    /// <summary>
    /// Matches by install-folder hint. The longest matching hint wins, so "call of duty modern warfare
    /// iii" beats "call of duty modern warfare ii" and "call of duty modern warfare".
    /// </summary>
    public static GameDefinition? MatchByName(string installDirOrName)
    {
        var s = installDirOrName.ToLowerInvariant();
        return All
            .SelectMany(g => g.InstallDirHints.Where(h => s.Contains(h)).Select(h => (Game: g, h.Length)))
            .OrderByDescending(m => m.Length)
            .Select(m => m.Game)
            .FirstOrDefault();
    }
}
