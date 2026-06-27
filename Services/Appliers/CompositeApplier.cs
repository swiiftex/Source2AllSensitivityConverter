namespace Source2AllSensitivityConverter.Services.Appliers;

/// <summary>
/// Applies a sensitivity to several places at once (e.g. a config with separate hipfire / ADS /
/// scope sensitivities). Each inner applier writes the same converted value; results are aggregated.
/// </summary>
public sealed class CompositeApplier(IReadOnlyList<IConfigApplier> appliers) : IConfigApplier
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

        return problems.Count == 0
            ? ApplyResult.Ok($"Wrote {ok} value(s).")
            : ApplyResult.Fail(string.Join(" | ", problems));
    }

    // Use the first target as the representative current value (e.g. for "Detect from game").
    public double? TryReadCurrent(string installPath)
        => appliers.Count > 0 ? appliers[0].TryReadCurrent(installPath) : null;
}
