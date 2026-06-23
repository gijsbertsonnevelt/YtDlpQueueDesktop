namespace YtDlpQueueDesktop;

/// <summary>
/// Bevat de versiegeschiedenis van yt-dlp Queue.
/// Voeg nieuwe versies toe bovenaan de lijst.
/// </summary>
public static class Changelog
{
    public static readonly IReadOnlyList<ChangelogEntry> Entries =
    [
        new("1.1.0", new DateOnly(2026, 6, 23),
        [
            new ChangelogSection("Nieuw", [
                "Helpvenster toegevoegd via ? knop in de hoofdbalk (gebruik en probleemoplossing)",
                "Changelog toegevoegd via Instellingen → Changelog",
                "Automatische retry bij YouTube 403-fout met aftellertje in de statuskolom",
                "Permanente fouten (privé, verwijderd, copyright) worden automatisch uit de wachtrij verwijderd",
                "Netwerkinterceptie detectie voor Zscaler/SSL-proxy met duidelijke melding",
                "WebView2 installatie vanuit de app als runtime ontbreekt",
            ]),
            new ChangelogSection("Verbeterd", [
                "MP4 download gebruikt nu H.264 video om 403-fouten bij VP9/AV1 te voorkomen",
                "Cookies worden altijd opgeslagen in AppData ongeacht debug of release build",
                "Tools-map PATH uitgebreid met Machine én User omgevingsvariabelen",
                "Sleep interval toegevoegd tussen downloads om rate limiting te verminderen",
                "Update-knoppen voor yt-dlp, ffmpeg en deno tonen huidige versie in kleur",
                "Instellingenvenster vereenvoudigd — hardcoded toolpaden verwijderd",
                "Statuskolom toont voortgang van retry aftelling bij 403-fout",
            ]),
            new ChangelogSection("Opgelost", [
                "Cookies-bestand pad mismatch tussen debug-build en geïnstalleerde versie",
                "Zoekvenster toonde geen foutmelding bij Zscaler SSL-blokkade",
                "Cookies vernieuwen popup verscheen onterecht bij gewone 403-fouten",
                "Dubbele AppData map (YtDlpQueue en yt-dlp Queue) bij nieuwe installatie",
                "WebView2 profielmap werd aangemaakt in verkeerde locatie",
            ]),
        ]),
        new("1.0.0", new DateOnly(2025, 6, 23),
        [
            new ChangelogSection("Nieuw", [
                "YouTube video zoeken via zoekveld",
                "YouTube playlist zoeken via zoekveld met uitroepteken (!)",
                "Zoekresultaten met thumbnail, kanaal en duur",
                "Videocount per playlist asynchroon ophalen",
                "Download wachtrij met parallelle downloads",
                "Video downloaden in beste kwaliteit",
                "Video downloaden als MP4",
                "Audio downloaden als MP3",
                "Playlist submap structuur (Zoekterm\\Playlistnaam\\video's)",
                "Automatisch verwijderen van voltooide downloads na 10 seconden",
                "Instellingen: outputmap, max. downloads, cookies",
                "Inloggen bij YouTube via ingebouwde browser",
                "Automatische update-controle voor yt-dlp, ffmpeg en deno",
                "MSI installer met perUser installatie",
            ]),
            new ChangelogSection("Foutafhandeling", [
                "Duidelijke melding bij corrupt cookies-bestand",
                "WebView2 Runtime installatie vanuit de app",
            ]),
        ]),
    ];
}

/// <summary>Eén versie-entry in de changelog.</summary>
public record ChangelogEntry(
    string Version,
    DateOnly Date,
    IReadOnlyList<ChangelogSection> Sections);

/// <summary>Eén sectie binnen een versie-entry (bijv. "Nieuw", "Verbeterd", "Opgelost").</summary>
public record ChangelogSection(
    string Title,
    IReadOnlyList<string> Items);