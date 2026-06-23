using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace YtDlpQueueDesktop.Services;

public record UpdateCheckResult(
    string CurrentVersion,
    string LatestVersion,
    string DownloadUrl,
    bool IsUpdateAvailable);

/// <summary>
/// Controleert of er een nieuwere versie van yt-dlp, ffmpeg of deno beschikbaar is op GitHub,
/// en kan die desgewenst downloaden en installeren in de tools-map.
/// De bestaande exe wordt hernoemd (niet verwijderd) zodat een rollback simpelweg het
/// terugzetten van de bestandsnaam is.
/// </summary>
public static class ToolUpdater
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(120),
    };

    // ── yt-dlp ────────────────────────────────────────────────────────────────

    public static async Task<UpdateCheckResult> CheckYtDlpAsync()
    {
        var current = await GetExeVersionAsync(ToolPaths.YtDlp, "--version");
        var (latest, url) = await GetYtDlpLatestAsync();

        return new UpdateCheckResult(
            current,
            latest,
            url,
            CompareVersions(latest, current) > 0);
    }

    public static Task<string> InstallYtDlpAsync(string downloadUrl, string currentVersion) =>
        DownloadAndReplaceAsync(ToolPaths.YtDlp, downloadUrl, currentVersion);

    // ── ffmpeg ────────────────────────────────────────────────────────────────

    public static async Task<UpdateCheckResult> CheckFfmpegAsync()
    {
        var current = await GetFfmpegVersionAsync();
        var (latest, url) = await GetFfmpegLatestAsync();

        return new UpdateCheckResult(
            current,
            latest,
            url,
            CompareVersions(latest, current) > 0);
    }

    public static Task<string> InstallFfmpegAsync(string downloadUrl, string currentVersion) =>
        DownloadZipAndReplaceExeAsync(
            targetPath: ToolPaths.Ffmpeg,
            downloadUrl: downloadUrl,
            currentVersion: currentVersion,
            expectedExeName: "ffmpeg.exe");

    // ── deno ──────────────────────────────────────────────────────────────────

    public static async Task<UpdateCheckResult> CheckDenoAsync()
    {
        var current = await GetDenoVersionAsync();
        var (latest, url) = await GetDenoLatestAsync();

        return new UpdateCheckResult(
            current,
            latest,
            url,
            CompareVersions(latest, current) > 0);
    }

    public static Task<string> InstallDenoAsync(string downloadUrl, string currentVersion) =>
        DownloadZipAndReplaceExeAsync(
            targetPath: ToolPaths.Deno,
            downloadUrl: downloadUrl,
            currentVersion: currentVersion,
            expectedExeName: "deno.exe");

    // ── GitHub helpers ────────────────────────────────────────────────────────

    private static async Task<(string Version, string Url)> GetYtDlpLatestAsync()
    {
        var json = await GetGitHubReleaseJsonAsync("yt-dlp/yt-dlp");

        using var doc = JsonDocument.Parse(json);

        var version = (doc.RootElement.GetProperty("tag_name").GetString() ?? "").TrimStart('v');
        string? url = null;

        foreach (var asset in doc.RootElement.GetProperty("assets").EnumerateArray())
        {
            if (string.Equals(
                asset.GetProperty("name").GetString(),
                "yt-dlp.exe",
                StringComparison.OrdinalIgnoreCase))
            {
                url = asset.GetProperty("browser_download_url").GetString();
                break;
            }
        }

        if (string.IsNullOrWhiteSpace(url))
            throw new InvalidOperationException("Kon yt-dlp.exe niet vinden in de GitHub release.");

        return (version, url);
    }

    private static async Task<(string Version, string Url)> GetFfmpegLatestAsync()
    {
        var json = await GetGitHubReleaseJsonAsync("GyanD/codexffmpeg");

        using var doc = JsonDocument.Parse(json);

        string? url = null;
        string? version = null;

        foreach (var asset in doc.RootElement.GetProperty("assets").EnumerateArray())
        {
            var name = asset.GetProperty("name").GetString() ?? "";

            // Voorbeeld:
            // ffmpeg-7.1.1-essentials_build.zip
            if (name.EndsWith("-essentials_build.zip", StringComparison.OrdinalIgnoreCase))
            {
                url = asset.GetProperty("browser_download_url").GetString();

                var match = Regex.Match(
                    name,
                    @"^ffmpeg-(?<ver>[0-9]+(?:\.[0-9]+)*)-essentials_build\.zip$",
                    RegexOptions.IgnoreCase);

                if (match.Success)
                    version = match.Groups["ver"].Value;

                break;
            }
        }

        if (string.IsNullOrWhiteSpace(url))
            throw new InvalidOperationException("Kon ffmpeg essentials build niet vinden in de GitHub release.");

        if (string.IsNullOrWhiteSpace(version))
        {
            // Fallback op tag_name als de assetnaam onverwacht anders is
            version = (doc.RootElement.GetProperty("tag_name").GetString() ?? "").TrimStart('v');
        }

        return (version, url);
    }

    private static async Task<(string Version, string Url)> GetDenoLatestAsync()
    {
        var json = await GetGitHubReleaseJsonAsync("denoland/deno");

        using var doc = JsonDocument.Parse(json);

        var version = (doc.RootElement.GetProperty("tag_name").GetString() ?? "").TrimStart('v');
        string? url = null;

        foreach (var asset in doc.RootElement.GetProperty("assets").EnumerateArray())
        {
            var name = asset.GetProperty("name").GetString() ?? "";

            if (name.Equals("deno-x86_64-pc-windows-msvc.zip", StringComparison.OrdinalIgnoreCase))
            {
                url = asset.GetProperty("browser_download_url").GetString();
                break;
            }
        }

        if (string.IsNullOrWhiteSpace(url))
            throw new InvalidOperationException("Kon deno-x86_64-pc-windows-msvc.zip niet vinden in de GitHub release.");

        return (version, url);
    }

    private static async Task<string> GetGitHubReleaseJsonAsync(string repo)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"https://api.github.com/repos/{repo}/releases/latest");

        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("YtDlpQueueDesktop", "1.0"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        using var response = await Http.SendAsync(request);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync();
    }

    // ── Versie ophalen ────────────────────────────────────────────────────────

    private static async Task<string> GetExeVersionAsync(string exePath, string args)
    {
        if (!File.Exists(exePath))
            return "onbekend";

        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var p = Process.Start(psi);
        if (p is null)
            return "onbekend";

        var output = await p.StandardOutput.ReadToEndAsync();
        var error = await p.StandardError.ReadToEndAsync();
        await p.WaitForExitAsync();

        var combined = string.IsNullOrWhiteSpace(output) ? error : output;
        return combined.Trim();
    }

    private static async Task<string> GetFfmpegVersionAsync()
    {
        if (!File.Exists(ToolPaths.Ffmpeg))
            return "onbekend";

        var psi = new ProcessStartInfo
        {
            FileName = ToolPaths.Ffmpeg,
            Arguments = "-version",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var p = Process.Start(psi);
        if (p is null)
            return "onbekend";

        var output = await p.StandardOutput.ReadToEndAsync();
        await p.WaitForExitAsync();

        // Eerste regel meestal:
        // ffmpeg version 7.1.1-essentials_build-www.gyan.dev Copyright ...
        var firstLine = output
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(firstLine))
            return "onbekend";

        var parts = firstLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var versionIndex = Array.IndexOf(parts, "version");

        if (versionIndex >= 0 && versionIndex + 1 < parts.Length)
        {
            var rawVersion = parts[versionIndex + 1];
            return rawVersion.Split('-')[0];
        }

        return "onbekend";
    }

    private static async Task<string> GetDenoVersionAsync()
    {
        if (!File.Exists(ToolPaths.Deno))
            return "onbekend";

        var psi = new ProcessStartInfo
        {
            FileName = ToolPaths.Deno,
            Arguments = "--version",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var p = Process.Start(psi);
        if (p is null)
            return "onbekend";

        var output = await p.StandardOutput.ReadToEndAsync();
        await p.WaitForExitAsync();

        // Eerste regel normaal:
        // deno 2.8.3
        var firstLine = output
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(firstLine))
            return "onbekend";

        var parts = firstLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
            return parts[1].Trim();

        return "onbekend";
    }

    // ── Download en installeer ────────────────────────────────────────────────

    /// <summary>
    /// Downloadt een losse exe, hernoemt de bestaande naar
    /// &lt;naam&gt;.oud-&lt;versie&gt;.exe en plaatst de nieuwe op de oorspronkelijke plek.
    /// Geeft het pad van de backup terug.
    /// </summary>
    private static async Task<string> DownloadAndReplaceAsync(
        string targetPath,
        string downloadUrl,
        string currentVersion)
    {
        var directory = Path.GetDirectoryName(targetPath)!;
        Directory.CreateDirectory(directory);

        var baseName = Path.GetFileNameWithoutExtension(targetPath);
        var ext = Path.GetExtension(targetPath);
        var safeVersion = MakeSafeFilePart(currentVersion);

        var tempPath = Path.Combine(directory, $"{baseName}.nieuw{ext}");
        var backupPath = Path.Combine(directory, $"{baseName}.oud-{safeVersion}{ext}");

        if (File.Exists(tempPath))
            File.Delete(tempPath);

        using (var response = await Http.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
        {
            response.EnsureSuccessStatusCode();

            await using var fs = File.Create(tempPath);
            await response.Content.CopyToAsync(fs);
        }

        if (File.Exists(backupPath))
            File.Delete(backupPath);

        if (File.Exists(targetPath))
            File.Move(targetPath, backupPath);

        File.Move(tempPath, targetPath);

        return backupPath;
    }

    /// <summary>
    /// Downloadt een ZIP-archief, zoekt daarin de gevraagde exe, hernoemt de bestaande exe
    /// naar &lt;naam&gt;.oud-&lt;versie&gt;.exe en plaatst de nieuwe exe op de oorspronkelijke plek.
    /// Geeft het pad van de backup terug.
    /// </summary>
    private static async Task<string> DownloadZipAndReplaceExeAsync(
        string targetPath,
        string downloadUrl,
        string currentVersion,
        string expectedExeName)
    {
        var directory = Path.GetDirectoryName(targetPath)!;
        Directory.CreateDirectory(directory);

        var baseName = Path.GetFileNameWithoutExtension(targetPath);
        var ext = Path.GetExtension(targetPath);
        var safeVersion = MakeSafeFilePart(currentVersion);

        var tempZipPath = Path.Combine(directory, $"{baseName}.download.zip");
        var extractDir = Path.Combine(directory, $"{baseName}.extract");
        var tempExePath = Path.Combine(directory, $"{baseName}.nieuw{ext}");
        var backupPath = Path.Combine(directory, $"{baseName}.oud-{safeVersion}{ext}");

        // Opruimen van eventuele rommel van een vorige mislukte poging
        if (File.Exists(tempZipPath))
            File.Delete(tempZipPath);

        if (File.Exists(tempExePath))
            File.Delete(tempExePath);

        if (Directory.Exists(extractDir))
            Directory.Delete(extractDir, recursive: true);

        // ZIP downloaden
        using (var response = await Http.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
        {
            response.EnsureSuccessStatusCode();

            await using var fs = File.Create(tempZipPath);
            await response.Content.CopyToAsync(fs);
        }

        // ZIP uitpakken
        ZipFile.ExtractToDirectory(tempZipPath, extractDir);

        // Gevraagde exe zoeken
        var extractedExe = Directory
            .GetFiles(extractDir, expectedExeName, SearchOption.AllDirectories)
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(extractedExe))
            throw new InvalidOperationException($"Kon {expectedExeName} niet vinden in het gedownloade archief.");

        // Eerst naar tijdelijke exe kopiëren
        File.Copy(extractedExe, tempExePath, overwrite: true);

        // Bestaande backup verwijderen als die al bestaat
        if (File.Exists(backupPath))
            File.Delete(backupPath);

        // Huidige exe back-uppen
        if (File.Exists(targetPath))
            File.Move(targetPath, backupPath);

        // Nieuwe exe plaatsen
        File.Move(tempExePath, targetPath);

        // Opruimen
        if (File.Exists(tempZipPath))
            File.Delete(tempZipPath);

        if (Directory.Exists(extractDir))
            Directory.Delete(extractDir, recursive: true);

        return backupPath;
    }

    // ── Versievergelijking ────────────────────────────────────────────────────

    /// <summary>
    /// Vergelijkt versienummers segment voor segment.
    /// Werkt voor bijvoorbeeld:
    /// - yt-dlp: 2026.06.01
    /// - ffmpeg: 7.1.1
    /// - deno: 2.8.3
    /// Retourneert > 0 als a nieuwer is dan b.
    /// </summary>
    public static int CompareVersions(string a, string b)
    {
        var versionA = NormalizeVersion(a);
        var versionB = NormalizeVersion(b);

        var partsA = versionA.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var partsB = versionB.Split('.', StringSplitOptions.RemoveEmptyEntries);

        var len = Math.Max(partsA.Length, partsB.Length);

        for (var i = 0; i < len; i++)
        {
            var pa = i < partsA.Length && int.TryParse(partsA[i], out var va) ? va : 0;
            var pb = i < partsB.Length && int.TryParse(partsB[i], out var vb) ? vb : 0;

            if (pa != pb)
                return pa.CompareTo(pb);
        }

        return 0;
    }

    private static string NormalizeVersion(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "0";

        value = value.Trim().TrimStart('v', 'V');

        // Pak de eerste dotted numeric sequence, bv:
        // "7.1.1-essentials_build" -> "7.1.1"
        // "v2.8.3" -> "2.8.3"
        // "2026.06.01" -> "2026.06.01"
        var match = Regex.Match(value, @"\d+(?:\.\d+)+");
        if (match.Success)
            return match.Value;

        // Fallback: als er alleen een los getal staat
        var matchSingle = Regex.Match(value, @"\d+");
        if (matchSingle.Success)
            return matchSingle.Value;

        return "0";
    }

    private static string MakeSafeFilePart(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "onbekend";

        var invalid = Path.GetInvalidFileNameChars();
        var chars = value.Select(c => invalid.Contains(c) ? '_' : c).ToArray();
        return new string(chars);
    }
}