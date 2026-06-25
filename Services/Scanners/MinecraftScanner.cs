using System.IO;
using Source2AllSensitivityConverter.Models;

namespace Source2AllSensitivityConverter.Services.Scanners;

/// <summary>
/// Minecraft (Java) has no store launcher we scan, so we detect it directly by the presence of
/// the default <c>%APPDATA%\.minecraft</c> folder and surface it as an installed game.
/// </summary>
public sealed class MinecraftScanner : IStoreScanner
{
    public Store Store => Store.Other;

    private readonly string _dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft");

    public bool IsAvailable => Directory.Exists(_dir);

    public IEnumerable<DetectedGame> Scan()
    {
        if (!IsAvailable) yield break;

        yield return new DetectedGame
        {
            DisplayName = "Minecraft",
            InstallPath = _dir,
            Store = Store.Other,
        };
    }
}
