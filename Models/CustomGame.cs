using Source2AllSensitivityConverter.Services;
using Source2AllSensitivityConverter.Services.Appliers;
using System.IO;

namespace Source2AllSensitivityConverter.Models;

/// <summary>
/// A game the user added manually through the "Manually add game" dialog. Persisted in settings and
/// turned into a live <see cref="GameDefinition"/> / <see cref="DetectedGame"/> at runtime.
/// </summary>
public sealed class CustomGame
{
    public string Name { get; set; } = "";

    /// <summary>This game's sensitivity value that feels the same as CS2 / Source sensitivity 1.0.</summary>
    public double EquivalentOfCs1 { get; set; } = 1.0;

    public string ConfigFolder { get; set; } = "";
    public string ConfigFile { get; set; } = "";

    /// <summary>One-line template with a <c>{value}</c> placeholder, e.g. <c>sensitivity "{value}"</c>.</summary>
    public string SensitivityLine { get; set; } = "";

    public string FullConfigPath => Path.Combine(ConfigFolder, ConfigFile);

    /// <summary>yaw such that <see cref="EquivalentOfCs1"/> maps to Source/CS2 sensitivity 1.0.</summary>
    public double Yaw => SensitivityConverter.SourceYaw / EquivalentOfCs1;

    /// <summary>True when the config folder/file/line are all set, enabling auto-apply.</summary>
    public bool HasAutoApply =>
        !string.IsNullOrWhiteSpace(ConfigFolder)
        && !string.IsNullOrWhiteSpace(ConfigFile)
        && !string.IsNullOrWhiteSpace(SensitivityLine);

    public GameDefinition ToDefinition() => new()
    {
        Name = Name,
        Engine = Engine.Other,
        YawConstant = EquivalentOfCs1 > 0 ? Yaw : null,
        Applier = HasAutoApply ? new TemplateApplier(FullConfigPath, SensitivityLine) : null,
    };

    public DetectedGame ToDetectedGame()
    {
        var def = ToDefinition();
        return new DetectedGame
        {
            DisplayName = Name,
            InstallPath = ConfigFolder,
            Store = Store.Other,
            Definition = def,
            DetectedEngine = Engine.Other,
        };
    }
}
