namespace Source2AllSensitivityConverter.Models;

/// <summary>Human-friendly display names for <see cref="Engine"/> values (no raw enum text in the UI).</summary>
public static class EngineNames
{
    public static string Display(Engine engine) => engine switch
    {
        Engine.Source => "Source",
        Engine.Source2 => "Source 2",
        Engine.GoldSrc => "GoldSrc",
        Engine.Unreal => "Unreal Engine",
        Engine.Unity => "Unity",
        Engine.IdTech => "id Tech",
        Engine.REDengine => "REDengine",
        Engine.Frostbite => "Frostbite",
        Engine.CallOfDuty => "Call of Duty (IW)",
        Engine.Riot => "Riot",
        Engine.Overwatch => "Overwatch",
        Engine.Other => "Other",
        _ => "Unknown",
    };
}
