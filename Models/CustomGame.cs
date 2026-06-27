using Source2AllSensitivityConverter.Services;
using Source2AllSensitivityConverter.Services.Appliers;
using System.IO;

namespace Source2AllSensitivityConverter.Models;

/// <summary>How a manually-added game's config gets written.</summary>
public enum AutoApplyMode
{
    None,          // convert-only
    TextTemplate,  // plain-text config; a {value} line template
    GvasString,    // GVAS .sav map value stored as text
    GvasNumeric,   // GVAS .sav map value stored as Int/Float/Double
}

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

    public AutoApplyMode Mode { get; set; } = AutoApplyMode.None;

    /// <summary>Text-template mode: a one-line template with a <c>{value}</c> placeholder.</summary>
    public string SensitivityLine { get; set; } = "";

    /// <summary>GVAS modes: the map key whose value holds the sensitivity.</summary>
    public string OptionKey { get; set; } = "";

    /// <summary>GVAS-numeric mode: how the value is stored.</summary>
    public GvasValueKind GvasKind { get; set; } = GvasValueKind.Unknown;

    public string FullConfigPath => Path.Combine(ConfigFolder, ConfigFile);

    private bool HasFile =>
        !string.IsNullOrWhiteSpace(ConfigFolder) && !string.IsNullOrWhiteSpace(ConfigFile);

    /// <summary>Resolve the effective mode, inferring TextTemplate for older saved entries.</summary>
    private AutoApplyMode EffectiveMode
    {
        get
        {
            if (Mode != AutoApplyMode.None) return Mode;
            return HasFile && !string.IsNullOrWhiteSpace(SensitivityLine)
                ? AutoApplyMode.TextTemplate
                : AutoApplyMode.None;
        }
    }

    public double Yaw => SensitivityConverter.SourceYaw / EquivalentOfCs1;

    public GameDefinition ToDefinition() => new()
    {
        Name = Name,
        Engine = Engine.Other,
        YawConstant = EquivalentOfCs1 > 0 ? Yaw : null,
        Applier = BuildApplier(),
    };

    private IConfigApplier? BuildApplier()
    {
        if (!HasFile) return null;
        return EffectiveMode switch
        {
            AutoApplyMode.TextTemplate => new TemplateApplier(FullConfigPath, SensitivityLine),
            AutoApplyMode.GvasString => new GvasStringMapApplier(_ => FullConfigPath, OptionKey),
            AutoApplyMode.GvasNumeric => new GvasMapNumericApplier(_ => FullConfigPath, OptionKey, GvasKind),
            _ => null,
        };
    }

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
