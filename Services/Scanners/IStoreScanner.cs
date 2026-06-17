using Source2AllSensitivityConverter.Models;

namespace Source2AllSensitivityConverter.Services.Scanners;

/// <summary>
/// Discovers installed games for a particular launcher/store by reading its config files
/// and registry entries. Scanners must be resilient: a missing store returns an empty list.
/// </summary>
public interface IStoreScanner
{
    Store Store { get; }

    /// <summary>True if this store appears to be installed at all.</summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Returns the raw installs found. Engine detection and catalog matching happen later in
    /// <see cref="InstalledGameScanner"/>; scanners only populate name/path/store/appid.
    /// </summary>
    IEnumerable<DetectedGame> Scan();
}
