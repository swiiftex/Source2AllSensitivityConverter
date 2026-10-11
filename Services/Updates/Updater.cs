using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;

namespace Source2AllSensitivityConverter.Services.Updates;

public sealed record ReleaseAsset(string Url, long Size, string? Sha256, bool IsZip);

/// <summary>A GitHub release and the build to download from it (the single-file exe, or the zip holding it).</summary>
public sealed record ReleaseInfo(AppVersion Version, string Tag, string Name, string PageUrl, ReleaseAsset? Download);

/// <summary>Checks the GitHub repository for newer releases and installs them in place.</summary>
public static class Updater
{
    public const string Repository = "swiiftex/Source2AllSensitivityConverter";
    public const string ExeName = "Source2AllSensitivityConverter.exe";
    public static string ReleasesPage => $"https://github.com/{Repository}/releases";

    private static readonly HttpClient Http = CreateClient();

    public static ReleaseInfo? Available { get; private set; }
    public static string? LastError { get; private set; }
    public static DateTime? LastChecked { get; private set; }
    public static bool IsChecking { get; private set; }

    /// <summary>Raised (on any thread) when a check starts or finishes.</summary>
    public static event Action? Changed;

    private static HttpClient CreateClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"Source2AllSensitivityConverter/{AppVersion.Current}");
        return http;
    }

    public static async Task<ReleaseInfo?> CheckAsync()
    {
        if (IsChecking) return Available;
        IsChecking = true;
        Changed?.Invoke();
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{Repository}/releases?per_page=30");
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            using var response = await Http.SendAsync(request, cts.Token);
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException(response.StatusCode == System.Net.HttpStatusCode.NotFound
                    ? "the release page isn't reachable"
                    : $"GitHub answered {(int)response.StatusCode}");
            Available = PickUpdate(await response.Content.ReadAsStringAsync(cts.Token), AppVersion.Current);
            LastError = null;
        }
        catch (Exception ex)
        {
            LastError = ex is OperationCanceledException or HttpRequestException ? "couldn't reach GitHub" : ex.Message;
        }
        finally
        {
            IsChecking = false;
            LastChecked = DateTime.Now;
            Changed?.Invoke();
        }
        return Available;
    }

    /// <summary>
    /// The newest published release above <paramref name="current"/>, from the GitHub releases JSON.
    /// Prereleases are only offered to people already running one.
    /// </summary>
    public static ReleaseInfo? PickUpdate(string json, AppVersion current)
    {
        using var doc = JsonDocument.Parse(json);
        ReleaseInfo? best = null;
        foreach (var release in doc.RootElement.EnumerateArray())
        {
            if (release.TryGetProperty("draft", out var draft) && draft.GetBoolean()) continue;
            string tag = release.GetProperty("tag_name").GetString() ?? "";
            if (!AppVersion.TryParse(tag, out var version)) continue;
            bool prerelease = version.IsPrerelease || (release.TryGetProperty("prerelease", out var pre) && pre.GetBoolean());
            if (prerelease && !current.IsPrerelease) continue;
            if (!(version > current) || (best != null && !(version > best.Version))) continue;

            ReleaseAsset? exe = null, zip = null;
            if (release.TryGetProperty("assets", out var assets))
                foreach (var asset in assets.EnumerateArray())
                {
                    string name = asset.GetProperty("name").GetString() ?? "";
                    bool isExe = name.Equals(ExeName, StringComparison.OrdinalIgnoreCase);
                    bool isZip = name.StartsWith("Source2AllSensitivityConverter", StringComparison.OrdinalIgnoreCase)
                                 && name.EndsWith("-win-x64.zip", StringComparison.OrdinalIgnoreCase);
                    if (!isExe && !isZip) continue;
                    var found = new ReleaseAsset(
                        asset.GetProperty("browser_download_url").GetString() ?? "",
                        asset.TryGetProperty("size", out var s) ? s.GetInt64() : 0,
                        asset.TryGetProperty("digest", out var d) && d.GetString() is string digest && digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)
                            ? digest[7..] : null,
                        isZip);
                    if (isExe) exe = found; else zip = found;
                }
            best = new ReleaseInfo(version, tag, release.TryGetProperty("name", out var n) && n.GetString() is { Length: > 0 } title ? title : tag,
                release.GetProperty("html_url").GetString() ?? ReleasesPage, exe ?? zip);
        }
        return best;
    }

    /// <summary>The running exe (the single-file build), which the update replaces.</summary>
    private static string CurrentExe => Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, ExeName);

    /// <summary>True if this copy can update itself to <paramref name="release"/> (it has a build and the folder is writable).</summary>
    public static bool CanInstall(ReleaseInfo release)
    {
        if (release.Download == null) return false;
        try
        {
            string probe = Path.Combine(AppContext.BaseDirectory, $".write-test-{Environment.ProcessId}");
            File.WriteAllText(probe, "");
            File.Delete(probe);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Downloads the update and returns the action that applies it. Run that action as the app exits: it starts a
    /// file swap that waits for this process to end, replaces the exe (settings in UserData are untouched) and
    /// starts the new version.
    /// </summary>
    public static async Task<Action> PrepareInstallAsync(ReleaseInfo release, IProgress<double> progress, CancellationToken ct)
    {
        var download = release.Download ?? throw new InvalidOperationException("This release has no Windows build.");
        string work = Path.Combine(Path.GetTempPath(), "Source2AllSensitivityConverter-update", release.Tag);
        if (Directory.Exists(work)) Directory.Delete(work, recursive: true);
        Directory.CreateDirectory(work);

        string newExe;
        if (download.IsZip)
        {
            string zip = Path.Combine(work, "update.zip");
            await DownloadAsync(download, zip, progress, ct);
            string files = Path.Combine(work, "files");
            await Task.Run(() => ZipFile.ExtractToDirectory(zip, files), ct);
            newExe = Directory.EnumerateFiles(files, ExeName, SearchOption.AllDirectories).FirstOrDefault()
                     ?? throw new InvalidOperationException($"The update doesn't contain {ExeName}.");
        }
        else
        {
            newExe = Path.Combine(work, ExeName);
            await DownloadAsync(download, newExe, progress, ct);
        }

        string script = Path.Combine(work, "apply.ps1");
        File.WriteAllText(script, ApplyScript);
        var psi = new ProcessStartInfo("powershell.exe") { UseShellExecute = false, CreateNoWindow = true };
        foreach (var arg in new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-WindowStyle", "Hidden", "-File", script,
                     "-ProcessId", Environment.ProcessId.ToString(), "-Source", newExe, "-Target", CurrentExe })
            psi.ArgumentList.Add(arg);
        return () => Process.Start(psi);
    }

    private static async Task DownloadAsync(ReleaseAsset asset, string path, IProgress<double> progress, CancellationToken ct)
    {
        using (var response = await Http.GetAsync(asset.Url, HttpCompletionOption.ResponseHeadersRead, ct))
        {
            response.EnsureSuccessStatusCode();
            long total = response.Content.Headers.ContentLength ?? asset.Size;
            await using var input = await response.Content.ReadAsStreamAsync(ct);
            await using var output = File.Create(path);
            var buffer = new byte[1 << 16];
            long done = 0;
            int read;
            while ((read = await input.ReadAsync(buffer, ct)) > 0)
            {
                await output.WriteAsync(buffer.AsMemory(0, read), ct);
                done += read;
                if (total > 0) progress.Report(Math.Min(1, done / (double)total));
            }
        }

        if (asset.Sha256 != null)
        {
            await using var file = File.OpenRead(path);
            string actual = Convert.ToHexString(await SHA256.HashDataAsync(file, ct));
            if (!actual.Equals(asset.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The download is damaged (its checksum doesn't match). Try again.");
        }
    }

    /// <summary>Waits for the app to exit, copies the new exe over it (keeping the old one if that fails), then starts it again.</summary>
    internal const string ApplyScript = """
        param([int]$ProcessId, [string]$Source, [string]$Target)
        $log = Join-Path $env:TEMP 'Source2AllSensitivityConverter-update\update.log'
        function Note($text) { Add-Content -Path $log -Value ("{0:yyyy-MM-dd HH:mm:ss} {1}" -f (Get-Date), $text) -ErrorAction SilentlyContinue }
        Wait-Process -Id $ProcessId -Timeout 60 -ErrorAction SilentlyContinue
        Start-Sleep -Milliseconds 500
        $done = $false
        for ($i = 0; $i -lt 20 -and -not $done; $i++) {
            try { Copy-Item -LiteralPath $Source -Destination $Target -Force -ErrorAction Stop; $done = $true }
            catch { Start-Sleep -Seconds 1 }
        }
        if ($done) { Note "installed into $Target" } else { Note "copying failed: $($Error[0])" }
        Start-Process -FilePath $Target
        """;
}
