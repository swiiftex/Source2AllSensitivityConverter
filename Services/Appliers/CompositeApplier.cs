namespace Source2AllSensitivityConverter.Services.Appliers;

/// <summary>
/// Applies a sensitivity to several places at once (e.g. a config with separate hipfire / ADS /
/// scope sensitivities, or a game that keeps separate MP and SP config files). Each inner applier
/// writes the same converted value; results are aggregated.
///
/// When <paramref name="requireAll"/> is false (the default is true), the apply succeeds as long as
/// at least one target was written — useful when some target files may not exist (e.g. a CoD game
/// that only has a multiplayer config).
/// </summary>
public sealed class CompositeApplier(IReadOnlyList<IConfigApplier> appliers, bool requireAll = true)
    : IConfigApplier
{
    public string TargetDescription => string.Join("  +  ", appliers.Select(a => a.TargetDescription));

    public ApplyResult Apply(string installPath, double sensitivity)
    {
        int ok = 0;
        var problems = new List<string>();
        foreach (var a in appliers)
        {
            var r = a.Apply(installPath, sensitivity);
            if (r.Success) ok++; else problems.Add(r.Message);
        }

        var succeeded = requireAll ? problems.Count == 0 : ok > 0;
        return succeeded
            ? ApplyResult.Ok($"Wrote {ok} value(s).")
            : ApplyResult.Fail(string.Join(" | ", problems));
    }

    // Use the first target as the representative current value (e.g. for "Detect from game").
    public double? TryReadCurrent(string installPath)
        => appliers.Count > 0 ? appliers[0].TryReadCurrent(installPath) : null;
}
