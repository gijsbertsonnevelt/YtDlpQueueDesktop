namespace YtDlpQueueDesktop.Models;


/// <summary>
/// Instellingen, opgeslagen als JSON in %LocalAppData%\GS Software\YtDlpQueue\config.json
/// </summary>
public class AppSettings
{
    /// <summary>Map waar downloads worden opgeslagen.</summary>
    public string OutputDirectory { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "YtDlpQueue");

    /// <summary>Aantal downloads dat gelijktijdig mag draaien. Wijziging vereist herstart van de app.</summary>
    public int MaxConcurrentDownloads { get; set; } = 3;

    /// <summary>Pad naar een handmatig geëxporteerd cookies.txt-bestand in de tools-map.
    /// Leeg = geen cookies gebruiken.</summary>
    public string CookiesFilePath { get; set; } = "";
}