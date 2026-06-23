namespace YtDlpQueueDesktop.Models;

/// <summary>
/// Één item in de downloadwachtrij. Wordt bijgehouden vanaf het moment van
/// toevoegen tot voltooid/mislukt/geannuleerd.
/// </summary>
public class DownloadItem
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Url { get; init; } = string.Empty;

    /// <summary>"video" of "audio".</summary>
    public string Format { get; init; } = "video";

    public string? Title { get; set; }
    public DownloadStatus Status { get; set; } = DownloadStatus.Wachtend;
    public double ProgressPercent { get; set; }
    public string? Speed { get; set; }
    public string? Eta { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime AddedAt { get; init; } = DateTime.Now;

    /// <summary>Submapnaam afgeleid van de playlist/show waar deze video bij hoort (bv. de
    /// titel van een playlist-URL). Null voor een losse video zonder playlist-context.</summary>
    public string? PlaylistFolder { get; init; }

    /// <summary>Gekoppeld aan de actieve download zodat annuleren het yt-dlp proces kan stoppen.</summary>
    public CancellationTokenSource? Cts { get; set; }
}