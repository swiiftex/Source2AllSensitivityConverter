namespace Source2AllSensitivityConverter.Services.Appliers;

/// <summary>Result of an attempt to write a sensitivity into a game's config.</summary>
public readonly record struct ApplyResult(bool Success, string Message)
{
    public static ApplyResult Ok(string message) => new(true, message);
    public static ApplyResult Fail(string message) => new(false, message);
}

/// <summary>
/// Knows how to persist a converted sensitivity to a specific game's configuration files.
/// Implementations should be idempotent (re-applying yields the same file state) and must
/// never throw — surface problems through <see cref="ApplyResult"/>.
/// </summary>
public interface IConfigApplier
{
    /// <summary>Human-readable description of what/where this writes, shown in the UI.</summary>
    string TargetDescription { get; }

    /// <summary>Write <paramref name="sensitivity"/> for the game installed at <paramref name="installPath"/>.</summary>
    ApplyResult Apply(string installPath, double sensitivity);

    /// <summary>Best-effort read of the current sensitivity from the game's config, if available.</summary>
    double? TryReadCurrent(string installPath);
}
