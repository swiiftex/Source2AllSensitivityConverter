using System.Reflection;

namespace Source2AllSensitivityConverter.Services.Updates;

/// <summary>A semantic version (1.2.3-beta.1); prereleases sort before the release they lead up to.</summary>
public sealed record AppVersion(int Major, int Minor, int Patch, string? Prerelease) : IComparable<AppVersion>
{
    public bool IsPrerelease => Prerelease != null;

    /// <summary>
    /// A local build: the project's placeholder version. Release builds get their version from CI
    /// (<c>-p:Version=1.0.&lt;run&gt;</c>), matching the release tag.
    /// </summary>
    public bool IsDevelopmentBuild => Major == 0 && Minor == 0 && Patch == 0;

    /// <summary>The running build's version (from the project's &lt;Version&gt;).</summary>
    public static AppVersion Current { get; } =
        TryParse(typeof(AppVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion, out var v)
            ? v
            : new AppVersion(0, 0, 0, null);

    /// <summary>Parses "1.2.3", "v1.0.42", "0.3.0-beta.2+abc123".</summary>
    public static bool TryParse(string? text, out AppVersion version)
    {
        version = new AppVersion(0, 0, 0, null);
        if (string.IsNullOrWhiteSpace(text)) return false;
        string s = text.Trim().TrimStart('v', 'V');
        int plus = s.IndexOf('+');
        if (plus >= 0) s = s[..plus];
        string? pre = null;
        int dash = s.IndexOf('-');
        if (dash >= 0)
        {
            pre = s[(dash + 1)..];
            s = s[..dash];
            if (pre.Length == 0) return false;
        }
        var parts = s.Split('.');
        if (parts.Length is 0 or > 3) return false;
        var numbers = new int[3];
        for (int i = 0; i < parts.Length; i++)
            if (!int.TryParse(parts[i], out numbers[i]) || numbers[i] < 0) return false;
        version = new AppVersion(numbers[0], numbers[1], numbers[2], pre);
        return true;
    }

    public int CompareTo(AppVersion? other)
    {
        if (other is null) return 1;
        int c = Major.CompareTo(other.Major);
        if (c == 0) c = Minor.CompareTo(other.Minor);
        if (c == 0) c = Patch.CompareTo(other.Patch);
        if (c != 0) return c;
        if (Prerelease == null || other.Prerelease == null)
            return Prerelease == null ? (other.Prerelease == null ? 0 : 1) : -1;

        var a = Prerelease.Split('.');
        var b = other.Prerelease.Split('.');
        for (int i = 0; i < Math.Min(a.Length, b.Length); i++)
        {
            bool an = int.TryParse(a[i], out int ai), bn = int.TryParse(b[i], out int bi);
            c = an && bn ? ai.CompareTo(bi)
                : an ? -1 : bn ? 1                                    // numbers sort before words
                : string.Compare(a[i], b[i], StringComparison.OrdinalIgnoreCase);
            if (c != 0) return c;
        }
        return a.Length.CompareTo(b.Length);
    }

    public static bool operator >(AppVersion a, AppVersion b) => a.CompareTo(b) > 0;
    public static bool operator <(AppVersion a, AppVersion b) => a.CompareTo(b) < 0;

    /// <summary>"1.0.42", "2.0 rc 1".</summary>
    public string Display => $"{Major}.{Minor}.{Patch}{(Prerelease != null ? " " + Prerelease.Replace('.', ' ') : "")}";

    public override string ToString() => $"{Major}.{Minor}.{Patch}{(Prerelease != null ? "-" + Prerelease : "")}";
}
