namespace Source2AllSensitivityConverter.Models;

/// <summary>
/// Game engines we know how to identify and, where possible, convert sensitivity for.
/// </summary>
public enum Engine
{
    Unknown,
    Source,      // GoldSrc successor: HL2, CS:S, TF2, CS:GO, L4D2, Portal, Garry's Mod, Apex/Titanfall (Source fork)
    Source2,     // CS2, Dota 2, Half-Life: Alyx, Deadlock
    GoldSrc,     // Half-Life 1, original CS 1.6
    Unreal,      // Unreal Engine 3/4/5
    Unity,       // Unity runtime
    IdTech,      // Quake / DOOM family
    REDengine,   // Witcher 3, Cyberpunk 2077
    Frostbite,   // Battlefield, some EA shooters
    CallOfDuty,  // IW / Treyarch engines
    Riot,        // Valorant (custom UE fork, but its own sens model)
    Overwatch,   // Blizzard's custom engine
}

/// <summary>
/// Where an installed game was discovered.
/// </summary>
public enum Store
{
    Unknown,
    Steam,
    Epic,
    Gog,
    Xbox,
    Origin,
    BattleNet,
    Riot,
}
