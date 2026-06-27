using Source2AllSensitivityConverter.Services;
using Source2AllSensitivityConverter.Services.Appliers;
using System.IO;
using System.Text.Json.Serialization;

namespace Source2AllSensitivityConverter.Models;

/// <summary>How a manually-added game's config gets written.</summary>
public enum AutoApplyMode
{
    None,          // convert-only
    TextTemplate,  // plain-text config; a {value} line template
    GvasString,    // GVAS .sav map value stored as text
    GvasNumeric,   // GVAS .sav map value stored as Int/Float/Double
}

/// <summary>One place in the config file where the sensitivity is written.</summary>
public sealed class SensitivityTarget
{
    public AutoApplyMode Mode { get; set; } = AutoApplyMode.None;
    public string SensitivityLine { get; set; } = "";   // TextTemplate: {value} template
    public string OptionKey { get; set; } = "";          // GVAS: map key
    public GvasValueKind GvasKind { get; set; } = GvasValueKind.Unknown; // GvasNumeric width

    /// <summary>Short label for the dialog's "will write" list.</summary>
    public string Describe() => Mode switch
    {
        AutoApplyMode.TextTemplate => $"text · {SensitivityLine}",
        AutoApplyMode.GvasString => $"text · {OptionKey}",
        AutoApplyMode.GvasNumeric => $"{GvasKind.Word()} · {OptionKey}",
        _ => "(none)",
    };
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

    /// <summary>All the places the sensitivity is written in the config (supports multiple values).</summary>
    public List<SensitivityTarget> Targets { get; set; } = [];

    // ---- legacy single-target fields (older saved settings) ----
    public AutoApplyMode Mode { get; set; } = AutoApplyMode.None;
    public string SensitivityLine { get; set; } = "";
    public string OptionKey { get; set; } = "";
    public GvasValueKind GvasKind { get; set; } = GvasValueKind.Unknown;

    [JsonIgnore]
    public string FullConfigPath => Path.Combine(ConfigFolder, ConfigFile);

    private bool HasFile =>
        !string.IsNullOrWhiteSpace(ConfigFolder) && !string.IsNullOrWhiteSpace(ConfigFile);

    /// <summary>Targets to write, migrating an older single-target entry if needed.</summary>
    private IReadOnlyList<SensitivityTarget> EffectiveTargets
    {
        get
        {
            if (Targets.Count > 0) return Targets;
            if (Mode != AutoApplyMode.None)
                return [new SensitivityTarget { Mode = Mode, SensitivityLine = SensitivityLine, OptionKey = OptionKey, GvasKind = GvasKind }];
            return [];
        }
    }

    [JsonIgnore]
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

        var appliers = EffectiveTargets
            .Select(BuildApplierFor)
            .OfType<IConfigApplier>()
            .ToList();

        return appliers.Count switch
        {
            0 => null,
            1 => appliers[0],
            _ => new CompositeApplier(appliers),
        };
    }

    private IConfigApplier? BuildApplierFor(SensitivityTarget t) => t.Mode switch
    {
        AutoApplyMode.TextTemplate => new TemplateApplier(FullConfigPath, t.SensitivityLine),
        AutoApplyMode.GvasString => new GvasStringMapApplier(_ => FullConfigPath, t.OptionKey),
        AutoApplyMode.GvasNumeric => new GvasMapNumericApplier(_ => FullConfigPath, t.OptionKey, t.GvasKind),
        _ => null,
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
