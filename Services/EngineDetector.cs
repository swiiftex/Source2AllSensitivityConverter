using System.IO;
using Source2AllSensitivityConverter.Models;

namespace Source2AllSensitivityConverter.Services;

/// <summary>
/// Best-effort identification of a game's engine purely from its on-disk file structure.
/// Used when an install does not match a catalog entry, so the UI can still show "what is this".
/// </summary>
public static class EngineDetector
{
    public static Engine Detect(string installPath)
    {
        if (string.IsNullOrEmpty(installPath) || !Directory.Exists(installPath))
            return Engine.Unknown;

        try
        {
            // Source 2: engine2.dll or a *.gi game-info file.
            if (AnyFile(installPath, "engine2.dll") || AnyFile(installPath, "gameinfo.gi"))
                return Engine.Source2;

            // Source 1: classic Valve layout (hl2.exe / engine.dll under bin + gameinfo.txt).
            if ((AnyFile(installPath, "hl2.exe") || HasFile(installPath, "bin", "engine.dll"))
                && AnyFile(installPath, "gameinfo.txt"))
                return Engine.Source;

            // GoldSrc: original Half-Life engine.
            if (AnyFile(installPath, "hl.exe") || AnyFile(installPath, "hw.dll"))
                return Engine.GoldSrc;

            // Unreal Engine: *.uproject, Engine/Binaries, or a *-Win64-Shipping.exe + Content/Paks.
            if (AnyFile(installPath, "*.uproject")
                || DirExists(installPath, "Engine", "Binaries")
                || AnyFile(installPath, "*-Win64-Shipping.exe")
                || AnyDir(installPath, "Paks"))
                return Engine.Unreal;

            // Unity: UnityPlayer.dll / GameAssembly.dll, or a *_Data folder.
            if (AnyFile(installPath, "UnityPlayer.dll")
                || AnyFile(installPath, "GameAssembly.dll")
                || AnyDir(installPath, "*_Data"))
                return Engine.Unity;

            // id Tech: vulkan/d3d shipping exes commonly named *vk.exe alongside base/ packs.
            if (AnyFile(installPath, "*vk.exe") && AnyDir(installPath, "base"))
                return Engine.IdTech;

            // REDengine: Cyberpunk / Witcher 3 layout.
            if (DirExists(installPath, "bin", "x64") &&
                (AnyFile(installPath, "Cyberpunk2077.exe") || AnyFile(installPath, "witcher3.exe")))
                return Engine.REDengine;
        }
        catch { /* unreadable folders, permissions, etc. */ }

        return Engine.Unknown;
    }

    // --- helpers (shallow, depth-limited so scanning stays fast) ---

    private static bool HasFile(string root, params string[] parts)
        => File.Exists(Path.Combine(new[] { root }.Concat(parts).ToArray()));

    private static bool DirExists(string root, params string[] parts)
        => Directory.Exists(Path.Combine(new[] { root }.Concat(parts).ToArray()));

    /// <summary>True if a file matching <paramref name="pattern"/> exists within ~2 levels.</summary>
    private static bool AnyFile(string root, string pattern)
        => EnumerateShallow(root, pattern, isFile: true).Any();

    private static bool AnyDir(string root, string pattern)
        => EnumerateShallow(root, pattern, isFile: false).Any();

    private static IEnumerable<string> EnumerateShallow(string root, string pattern, bool isFile)
    {
        var opts = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            MaxRecursionDepth = 3,
            IgnoreInaccessible = true,
            MatchType = MatchType.Simple,
        };
        return isFile
            ? Directory.EnumerateFiles(root, pattern, opts)
            : Directory.EnumerateDirectories(root, pattern, opts);
    }
}
