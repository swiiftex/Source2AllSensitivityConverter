namespace Source2AllSensitivityConverter.Services.Appliers;

/// <summary>
/// Wraps a writer whose stored value is on a different scale from the in-game setting the app shows
/// (e.g. a config holding the slider × 100, or a game that stores the turn multiplier the slider maps
/// to). <paramref name="toStored"/> converts the shown value before writing; <paramref name="fromStored"/>
/// converts back when reading the current value.
/// </summary>
public sealed class TransformApplier(IConfigApplier inner, Func<double, double> toStored, Func<double, double> fromStored)
    : IConfigApplier
{
    /// <summary>A stored value that is the shown value times <paramref name="scale"/>.</summary>
    public static TransformApplier Scaled(IConfigApplier inner, double scale)
        => new(inner, v => v * scale, v => v / scale);

    public string TargetDescription => inner.TargetDescription;

    public ApplyResult Apply(string installPath, double sensitivity)
        => inner.Apply(installPath, toStored(sensitivity));

    public double? TryReadCurrent(string installPath)
        => inner.TryReadCurrent(installPath) is { } v ? fromStored(v) : null;
}
