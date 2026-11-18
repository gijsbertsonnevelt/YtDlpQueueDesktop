using System.IO;

namespace YtDlpQueueDesktop;

/// <summary>
/// Paden naar tools en cookies.
/// De tools zitten altijd in de map naast de exe (tools-submap).
/// De cookies worden altijd opgeslagen in de vaste AppData map,
/// zodat debug- en release-builds dezelfde cookies gebruiken.
/// </summary>
public static class ToolPaths
{
    public static readonly string BaseDir =
        AppContext.BaseDirectory;

    public static readonly string ToolsDir =
        Path.Combine(BaseDir, "tools");

    /// <summary>Vaste AppData map voor gebruikersdata, onafhankelijk van de exe-locatie.
    /// Zo gebruiken debug-build, release-build en geïnstalleerde versie altijd dezelfde cookies.</summary>
    public static readonly string AppDataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GS Software", "yt-dlp Queue");

    /// <summary>Cookies worden altijd opgeslagen in AppData, niet naast de exe.</summary>
    public static readonly string CookiesPath =
        Path.Combine(AppDataDir, "tools", "cookies.txt");

    public static string YtDlp => Path.Combine(ToolsDir, "yt-dlp.exe");
    public static string Ffmpeg => Path.Combine(ToolsDir, "ffmpeg.exe");
    public static string Deno => Path.Combine(ToolsDir, "deno.exe");

    /// <summary>Pad naar het Zscaler CA certificaat in PEM formaat.
    /// Wordt meegegeven aan yt-dlp via --ca-certificate als het bestand bestaat.</summary>
    public static readonly string ZscalerCertPath =
        Path.Combine(AppDataDir, "tools", "zscaler-ca.pem");

    public static void EnsureToolsDir()
    {
        if (!Directory.Exists(ToolsDir))
            Directory.CreateDirectory(ToolsDir);
    }

    public static (bool Ok, string[] Missing) Check()
    {
        var candidates = new (string Name, string Path)[]
        {
            ("yt-dlp.exe", YtDlp),
            ("ffmpeg.exe", Ffmpeg),
            ("deno.exe",   Deno),
        };

        var missing = candidates
            .Where(c => !File.Exists(c.Path))
            .Select(c => c.Name)
            .ToArray();

        return (missing.Length == 0, missing);
    }
}