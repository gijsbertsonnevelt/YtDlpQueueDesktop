using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using YtDlpQueueDesktop.Models;

namespace YtDlpQueueDesktop.Services;

/// <summary>Wordt gegooid als yt-dlp aangeeft dat het cookies-bestand ongeldig of corrupt is.</summary>
public class CookiesFileException(string message) : Exception(message);

/// <summary>Wordt gegooid als netwerkverkeer mogelijk wordt onderschept/geblokkeerd (bijv. proxy/Zscaler).</summary>
public class NetworkInterceptException(string message) : Exception(message);

/// <summary>Resultaat van het uitlezen van een URL (elke door yt-dlp ondersteunde site): de losse video-URL's, en (indien het om
/// een playlist/show/etc. ging) een gesaneerde naam om als submap te gebruiken.</summary>
public record ExpandResult(List<string> Urls, string? PlaylistFolder);

/// <summary>Eén zoekresultaat van een zoekopdracht (YouTube of een andere site).</summary>
public record SearchResultItem(
    string Id,
    string Url,
    string Title,
    string? Channel,
    string? Duration,
    string? ThumbnailUrl,
    bool IsPlaylist = false,
    int? VideoCount = null);

/// <summary>
/// Beheert de downloadwachtrij. Start bij <see cref="Start"/> een vast aantal "workers" die
/// items uit een Channel pakken en parallel laten downloaden via yt-dlp. De UI (MainForm)
/// luistert op <see cref="OnChange"/> om live te verversen.
/// </summary>
public class DownloadQueueService
{
    private readonly Channel<DownloadItem> _channel = Channel.CreateUnbounded<DownloadItem>();
    private readonly ConcurrentDictionary<Guid, DownloadItem> _items = new();
    private readonly CancellationTokenSource _appCts = new();
    private AppSettings _settings;
    private Task[] _workers = [];

    public DownloadQueueService(AppSettings settings)
    {
        _settings = settings;
    }

    public event Action? OnChange;

    public int MaxConcurrent => Math.Max(1, _settings.MaxConcurrentDownloads);

    public IReadOnlyList<DownloadItem> Items => _items.Values
        .OrderBy(i => i.AddedAt)
        .ToList();

    public int ActiveCount => _items.Values.Count(i => i.Status == DownloadStatus.Bezig);

    public void UpdateSettings(AppSettings settings) => _settings = settings;

    public void Start()
    {
        _workers = Enumerable.Range(0, MaxConcurrent)
            .Select(_ => Task.Run(() => WorkerLoopAsync(_appCts.Token)))
            .ToArray();
    }

    public void Shutdown()
    {
        _appCts.Cancel();

        foreach (var item in _items.Values)
        {
            if (item.Status != DownloadStatus.Voltooid)
                CleanupFiles(item);
        }
    }

    public void Enqueue(string url, string format, string? playlistFolder = null)
    {
        if (string.IsNullOrWhiteSpace(url))
            return;

        var item = new DownloadItem
        {
            Url = url.Trim(),
            Format = format,
            PlaylistFolder = playlistFolder
        };

        _items[item.Id] = item;
        _channel.Writer.TryWrite(item);
        NotifyChange();
    }

    public void Cancel(Guid id)
    {
        if (!_items.TryGetValue(id, out var item))
            return;

        item.Cts?.Cancel();

        if (item.Status == DownloadStatus.Wachtend)
        {
            item.Status = DownloadStatus.Geannuleerd;
            NotifyChange();
        }
    }

    public void Remove(Guid id)
    {
        if (_items.TryGetValue(id, out var item) && item.Status != DownloadStatus.Bezig)
        {
            if (item.Status is DownloadStatus.Mislukt or DownloadStatus.Geannuleerd)
                CleanupFiles(item);

            _items.TryRemove(id, out _);
            NotifyChange();
        }
    }

    public void Retry(Guid id)
    {
        if (!_items.TryGetValue(id, out var item))
            return;

        if (item.Status == DownloadStatus.Bezig)
            return;

        _items.TryRemove(id, out _);
        CleanupFiles(item);
        Enqueue(item.Url, item.Format, item.PlaylistFolder);
    }

    private async Task WorkerLoopAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var item in _channel.Reader.ReadAllAsync(stoppingToken))
            {
                if (item.Status == DownloadStatus.Geannuleerd)
                    continue;

                await RunDownloadAsync(item, stoppingToken);
            }
        }
        catch (OperationCanceledException) { }
    }

    private async Task RunDownloadAsync(DownloadItem item, CancellationToken appStoppingToken)
    {
        item.Status = DownloadStatus.Bezig;
        item.Cts = CancellationTokenSource.CreateLinkedTokenSource(appStoppingToken);
        NotifyChange();

        try
        {
            Directory.CreateDirectory(GetOutputDirectory(item));
        }
        catch (Exception ex)
        {
            item.Status = DownloadStatus.Mislukt;
            item.ErrorMessage = $"Kan outputmap niet aanmaken: {ex.Message}";
            NotifyChange();
            return;
        }

        var cookieRefreshAttempted = false;

        while (true)
        {
            var psi = new ProcessStartInfo
            {
                FileName = ToolPaths.YtDlp,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            psi.EnvironmentVariables["PATH"] =
                ToolPaths.ToolsDir + ";" + (Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine) ?? "") + ";" + (Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? "");

            // Zscaler CA certificaat meegeven via omgevingsvariabele zodat yt-dlp
            // SSL-verbindingen kan verifiëren op bedrijfsnetwerken met SSL-inspectie.
            if (File.Exists(ToolPaths.ZscalerCertPath))
            {
                psi.EnvironmentVariables["SSL_CERT_FILE"] = ToolPaths.ZscalerCertPath;
                psi.EnvironmentVariables["REQUESTS_CA_BUNDLE"] = ToolPaths.ZscalerCertPath;
            }

            foreach (var arg in BuildArguments(item))
                psi.ArgumentList.Add(arg);

            using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

            var stdoutBuffer = new StringBuilder();
            var stderrBuffer = new StringBuilder();

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data is null) return;
                lock (stdoutBuffer) stdoutBuffer.AppendLine(e.Data);
                HandleOutputLine(item, e.Data);
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is null) return;
                lock (stderrBuffer) stderrBuffer.AppendLine(e.Data);
                HandleOutputLine(item, e.Data);
            };

            using var killOnCancel = item.Cts.Token.Register(() =>
            {
                try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
                catch { }
            });

            try
            {
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await process.WaitForExitAsync(CancellationToken.None);

                var stdout = stdoutBuffer.ToString();
                var stderr = stderrBuffer.ToString();

                if (item.Cts.IsCancellationRequested)
                {
                    item.Status = DownloadStatus.Geannuleerd;
                    break;
                }

                if (process.ExitCode == 0)
                {
                    item.Status = DownloadStatus.Voltooid;
                    item.ProgressPercent = 100;
                    item.ErrorMessage = null;
                    _ = ScheduleAutoRemoveAsync(item);
                    break;
                }

                if (!cookieRefreshAttempted && (LooksLikeYoutube403OrAuthIssue(stderr, stdout) || LooksLikeYoutube403OrAuthIssue(item.ErrorMessage ?? "", "")))
                {
                    cookieRefreshAttempted = true;
                    item.Speed = null;
                    item.Eta = null;

                    // Aftellen met statusupdate elke seconde
                    var cancelled = false;
                    for (var remaining = 15; remaining > 0; remaining--)
                    {
                        item.ErrorMessage = $"HTTP 403 / toegang geweigerd — opnieuw proberen over {remaining}s...";
                        NotifyChange();
                        try
                        {
                            await Task.Delay(TimeSpan.FromSeconds(1), item.Cts!.Token);
                        }
                        catch (OperationCanceledException)
                        {
                            item.Status = DownloadStatus.Geannuleerd;
                            cancelled = true;
                            break;
                        }
                    }

                    if (cancelled) break;

                    item.ErrorMessage = null;
                    item.ProgressPercent = 0;
                    item.Speed = null;
                    item.Eta = null;
                    NotifyChange();
                    continue;
                }

                // Permanente fouten — video is niet beschikbaar, geen zin in retry
                if (IsPermanentError(stderr, stdout, item.ErrorMessage))
                {
                    _items.TryRemove(item.Id, out _);
                    NotifyChange();
                    return;
                }

                item.Status = DownloadStatus.Mislukt;
                item.ErrorMessage ??= $"yt-dlp gaf exitcode {process.ExitCode}.";
                break;
            }
            catch (Exception ex)
            {
                item.Status = DownloadStatus.Mislukt;
                item.ErrorMessage = ex.Message;
                break;
            }
        }

        NotifyChange();
    }

    private async Task ScheduleAutoRemoveAsync(DownloadItem item)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(10), _appCts.Token);

            if (_items.TryGetValue(item.Id, out var existing) &&
                existing.Status == DownloadStatus.Voltooid)
            {
                _items.TryRemove(item.Id, out _);
                NotifyChange();
            }
        }
        catch (OperationCanceledException) { }
        catch { }
    }

    private List<string> BuildArguments(DownloadItem item)
    {
        var outputTemplate = Path.Combine(GetOutputDirectory(item), "%(title)s.%(ext)s");

        var args = new List<string>
        {
            "--newline",
            "--no-color",
            "--no-playlist",
            "-o", outputTemplate,
            // Sommige sites hebben zeer lange titels; voorkom Windows-padlimiet problemen.
            "--trim-filenames", "150",
            "--sleep-interval", "2",
            "--max-sleep-interval", "5",
        };

        AddCookieArgs(args);

        args.Add("--ffmpeg-location");
        args.Add(ToolPaths.ToolsDir);

        if (item.Format == "audio")
        {
            args.Add("-f");
            args.Add("bestaudio[ext=m4a]/bestaudio");
            args.Add("-x");
            args.Add("--audio-format");
            args.Add("mp3");
            args.Add("--audio-quality");
            args.Add("0");
        }
        else if (item.Format == "mp4")
        {
            // Gebruik H.264 (avc) video + AAC audio — vermijdt VP9/AV1 die soms geblokkeerd worden
            args.Add("-f");
            args.Add("bestvideo[vcodec^=avc][ext=mp4]+bestaudio[ext=m4a]/bestvideo[ext=mp4]+bestaudio[ext=m4a]/best[ext=mp4]/best");
            args.Add("--merge-output-format");
            args.Add("mp4");
        }
        else
        {
            args.Add("-f");
            args.Add("bestvideo+bestaudio/best");
        }

        args.Add(item.Url);
        return args;
    }

    private string GetOutputDirectory(DownloadItem item) =>
        string.IsNullOrEmpty(item.PlaylistFolder)
            ? _settings.OutputDirectory
            : Path.Combine(_settings.OutputDirectory, item.PlaylistFolder);

    private void CleanupFiles(DownloadItem item)
    {
        if (string.IsNullOrEmpty(item.Title)) return;

        string dir;
        try
        {
            dir = GetOutputDirectory(item);
            if (!Directory.Exists(dir)) return;
        }
        catch { return; }

        foreach (var file in Directory.EnumerateFiles(dir))
        {
            var name = Path.GetFileName(file);
            if (!name.StartsWith(item.Title, StringComparison.OrdinalIgnoreCase)) continue;
            if (name.Length > item.Title.Length && name[item.Title.Length] != '.') continue;

            try { File.Delete(file); }
            catch { }
        }
    }

    private void AddCookieArgs(List<string> args)
    {
        var cookiesPath = File.Exists(ToolPaths.CookiesPath)
            ? ToolPaths.CookiesPath
            : _settings.CookiesFilePath?.Trim();

        if (!string.IsNullOrWhiteSpace(cookiesPath) && File.Exists(cookiesPath))
        {
            args.Add("--cookies");
            args.Add(cookiesPath);
        }

        // Op bedrijfsnetwerken met SSL-inspectie (bijv. Zscaler) kan yt-dlp de
        // certificaatketen niet verifiëren. Als het Zscaler PEM bestand aanwezig is
        // proberen we het via omgevingsvariabelen mee te geven (werkt niet altijd op
        // Windows), als fallback schakelen we certificaatverificatie uit.
        if (File.Exists(ToolPaths.ZscalerCertPath))
            args.Add("--no-check-certificates");
    }

    public Task<ExpandResult> ExpandUrlAsync(string url) => ExpandPlaylistAsync(url);

    private async Task<ExpandResult> ExpandPlaylistAsync(string url)
    {
        var args = new List<string> { "--flat-playlist", "--dump-json", "--no-warnings" };
        AddCookieArgs(args);
        args.Add(url);

        var psi = new ProcessStartInfo
        {
            FileName = ToolPaths.YtDlp,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        psi.EnvironmentVariables["PATH"] =
            ToolPaths.ToolsDir + ";" + (Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine) ?? "") + ";" + (Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? "");

        if (File.Exists(ToolPaths.ZscalerCertPath))
        {
            psi.EnvironmentVariables["SSL_CERT_FILE"] = ToolPaths.ZscalerCertPath;
            psi.EnvironmentVariables["REQUESTS_CA_BUNDLE"] = ToolPaths.ZscalerCertPath;
        }

        foreach (var arg in args)
            psi.ArgumentList.Add(arg);

        try
        {
            using var process = Process.Start(psi);
            if (process is null) return new ExpandResult([url], null);

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            var output = await outputTask;
            var stderr = await stderrTask;

            CheckForCookieError(stderr);
            CheckForNetworkInterception(stderr);

            var urls = new List<string>();
            string? playlistFolder = null;

            foreach (var line in output.Split('\n'))
            {
                var trimmed = line.Trim();
                if (trimmed.Length == 0) continue;

                try
                {
                    using var doc = JsonDocument.Parse(trimmed);
                    var root = doc.RootElement;

                    var id = root.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
                    var entryUrl = root.TryGetProperty("url", out var urlProp) ? urlProp.GetString() : null;

                    if (playlistFolder is null)
                    {
                        var title = root.TryGetProperty("playlist_title", out var titleProp) ? titleProp.GetString() : null;
                        if (string.IsNullOrWhiteSpace(title))
                            title = root.TryGetProperty("playlist", out var playlistProp) ? playlistProp.GetString() : null;
                        if (!string.IsNullOrWhiteSpace(title))
                            playlistFolder = SanitizeFolderName(title);
                    }

                    var resolved = ResolveEntryUrl(root, id, entryUrl);
                    if (resolved is not null)
                        urls.Add(resolved);
                }
                catch (JsonException) { }
            }

            return urls.Count > 0
                ? new ExpandResult(urls, playlistFolder)
                : new ExpandResult([url], null);
        }
        catch (CookiesFileException) { throw; }
        catch (NetworkInterceptException) { throw; }
        catch
        {
            return new ExpandResult([url], null);
        }
    }

    /// <summary>
    /// Bepaalt de pagina-URL van één item uit de <c>--dump-json</c> output. Werkt voor elke
    /// site die yt-dlp ondersteunt, niet alleen YouTube:
    /// 1. <c>webpage_url</c> — aanwezig bij een losse video (volledige info).
    /// 2. <c>url</c> bij playlist-entries (<c>_type</c> = url / url_transparent) — dat is
    ///    daar de pagina-URL. Bij een losse video kan <c>url</c> juist de directe
    ///    stream-URL zijn, daarom alleen voor entries.
    /// 3. Alleen voor YouTube: watch-URL opbouwen uit het video-id.
    /// Geeft null terug als er niets bruikbaars is; de aanroeper valt dan terug op de
    /// oorspronkelijke invoer-URL.
    /// </summary>
    private static string? ResolveEntryUrl(JsonElement root, string? id, string? entryUrl)
    {
        static bool IsHttp(string? value) =>
            !string.IsNullOrWhiteSpace(value) &&
            value.StartsWith("http", StringComparison.OrdinalIgnoreCase);

        var webpageUrl = root.TryGetProperty("webpage_url", out var wpProp) ? wpProp.GetString() : null;
        if (IsHttp(webpageUrl))
            return webpageUrl;

        var type = root.TryGetProperty("_type", out var typeProp) ? typeProp.GetString() : null;
        var isEntry = type is "url" or "url_transparent";
        if (isEntry && IsHttp(entryUrl))
            return entryUrl;

        var extractor =
            (root.TryGetProperty("ie_key", out var ieProp) ? ieProp.GetString() : null) ??
            (root.TryGetProperty("extractor_key", out var exProp) ? exProp.GetString() : null) ??
            (root.TryGetProperty("extractor", out var exNameProp) ? exNameProp.GetString() : null);

        var isYoutube = extractor is not null &&
                        extractor.StartsWith("youtube", StringComparison.OrdinalIgnoreCase);

        if (isYoutube && !string.IsNullOrEmpty(id))
            return $"https://www.youtube.com/watch?v={id}";

        return null;
    }

    public static string SanitizeFolderName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Where(c => !invalid.Contains(c)).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "Playlist" : sanitized;
    }

    public async Task<List<SearchResultItem>> SearchAsync(string query, int count = 12)
    {
        var results = new List<SearchResultItem>();
        if (string.IsNullOrWhiteSpace(query)) return results;

        var args = new List<string> { "--flat-playlist", "--dump-json", "--no-warnings" };
        AddCookieArgs(args);
        args.Add($"ytsearch{count}:{query}");

        var psi = new ProcessStartInfo
        {
            FileName = ToolPaths.YtDlp,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        psi.EnvironmentVariables["PATH"] =
            ToolPaths.ToolsDir + ";" + (Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine) ?? "") + ";" + (Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? "");

        if (File.Exists(ToolPaths.ZscalerCertPath))
        {
            psi.EnvironmentVariables["SSL_CERT_FILE"] = ToolPaths.ZscalerCertPath;
            psi.EnvironmentVariables["REQUESTS_CA_BUNDLE"] = ToolPaths.ZscalerCertPath;
        }

        foreach (var arg in args)
            psi.ArgumentList.Add(arg);

        using var process = Process.Start(psi);
        if (process is null) return results;

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var output = await outputTask;
        var stderr = await stderrTask;

        CheckForCookieError(stderr);
        CheckForNetworkInterception(stderr);

        foreach (var line in output.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0) continue;

            try
            {
                using var doc = JsonDocument.Parse(trimmed);
                var root = doc.RootElement;

                var id = root.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
                if (string.IsNullOrEmpty(id)) continue;

                var title = root.TryGetProperty("title", out var titleProp) ? titleProp.GetString() : null;
                var channel = root.TryGetProperty("channel", out var channelProp) ? channelProp.GetString() : null;
                if (string.IsNullOrEmpty(channel))
                    channel = root.TryGetProperty("uploader", out var uploaderProp) ? uploaderProp.GetString() : null;

                var duration = root.TryGetProperty("duration_string", out var durationProp) ? durationProp.GetString() : null;
                var thumbnailUrl = $"https://i.ytimg.com/vi/{id}/mqdefault.jpg";

                results.Add(new SearchResultItem(id, $"https://www.youtube.com/watch?v={id}",
                    title ?? id, channel, duration, thumbnailUrl));
            }
            catch (JsonException) { }
        }

        return results;
    }

    /// <summary>
    /// Zoekt op een willekeurige site uit <see cref="SearchProviders"/>. Voor YouTube wordt de
    /// bestaande <see cref="SearchAsync(string, int)"/> gebruikt (met YouTube-thumbnails).
    /// </summary>
    public async Task<List<SearchResultItem>> SearchAsync(SearchProvider provider, string query, int count = 12)
    {
        if (provider.IsYoutube)
            return await SearchAsync(query, count);

        var results = new List<SearchResultItem>();
        if (string.IsNullOrWhiteSpace(query)) return results;

        var args = new List<string> { "--flat-playlist", "--dump-json", "--no-warnings" };
        AddCookieArgs(args);

        if (provider.UrlTemplate is not null)
        {
            // Zoekpagina als playlist: alleen de eerste resultaten ophalen, niet alle pagina's.
            args.Add("--lazy-playlist");
            args.Add("--playlist-items");
            args.Add($"1:{count}");
        }

        args.Add(provider.BuildTarget(query, count));

        var psi = new ProcessStartInfo
        {
            FileName = ToolPaths.YtDlp,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        psi.EnvironmentVariables["PATH"] =
            ToolPaths.ToolsDir + ";" + (Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine) ?? "") + ";" + (Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? "");

        if (File.Exists(ToolPaths.ZscalerCertPath))
        {
            psi.EnvironmentVariables["SSL_CERT_FILE"] = ToolPaths.ZscalerCertPath;
            psi.EnvironmentVariables["REQUESTS_CA_BUNDLE"] = ToolPaths.ZscalerCertPath;
        }

        foreach (var arg in args)
            psi.ArgumentList.Add(arg);

        using var process = Process.Start(psi);
        if (process is null) return results;

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var output = await outputTask;
        var stderr = await stderrTask;

        CheckForCookieError(stderr);
        CheckForNetworkInterception(stderr);

        foreach (var line in output.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0) continue;

            try
            {
                using var doc = JsonDocument.Parse(trimmed);
                var root = doc.RootElement;

                var id = root.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
                var entryUrl = root.TryGetProperty("url", out var urlProp) ? urlProp.GetString() : null;

                // Zelfde logica als bij het uitlezen van playlists: werkt voor elke site.
                var url = ResolveEntryUrl(root, id, entryUrl);
                if (url is null) continue;

                var title = root.TryGetProperty("title", out var titleProp) ? titleProp.GetString() : null;
                var channel = root.TryGetProperty("channel", out var channelProp) ? channelProp.GetString() : null;
                if (string.IsNullOrEmpty(channel))
                    channel = root.TryGetProperty("uploader", out var uploaderProp) ? uploaderProp.GetString() : null;

                results.Add(new SearchResultItem(
                    id ?? url,
                    url,
                    string.IsNullOrWhiteSpace(title) ? url : title,
                    channel,
                    GetDurationText(root),
                    GetThumbnailUrl(root)));
            }
            catch (JsonException) { }
        }

        if (results.Count == 0 && process.ExitCode != 0)
        {
            var error = stderr.Split('\n')
                .Select(l => l.Trim())
                .LastOrDefault(l => l.Contains("ERROR", StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(error))
                throw new InvalidOperationException(error);
        }

        return results;
    }

    /// <summary>Duur als tekst: "duration_string" of anders berekend uit "duration" (seconden).</summary>
    private static string? GetDurationText(JsonElement root)
    {
        if (root.TryGetProperty("duration_string", out var ds) && ds.ValueKind == JsonValueKind.String)
            return ds.GetString();

        if (root.TryGetProperty("duration", out var d) && d.ValueKind == JsonValueKind.Number &&
            d.TryGetDouble(out var seconds) && seconds > 0)
        {
            var t = TimeSpan.FromSeconds(seconds);
            return t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss") : t.ToString(@"m\:ss");
        }

        return null;
    }

    /// <summary>Thumbnail: "thumbnail", of anders de laatste (meestal grootste) uit "thumbnails".</summary>
    private static string? GetThumbnailUrl(JsonElement root)
    {
        if (root.TryGetProperty("thumbnail", out var t) && t.ValueKind == JsonValueKind.String)
            return t.GetString();

        if (root.TryGetProperty("thumbnails", out var list) && list.ValueKind == JsonValueKind.Array)
        {
            string? last = null;
            foreach (var item in list.EnumerateArray())
            {
                if (item.TryGetProperty("url", out var u) && u.ValueKind == JsonValueKind.String)
                    last = u.GetString();
            }
            return last;
        }

        return null;
    }

    public async Task<List<SearchResultItem>> SearchPlaylistsAsync(string query, int count = 12)
    {
        var results = new List<SearchResultItem>();
        if (string.IsNullOrWhiteSpace(query)) return results;

        var encodedQuery = Uri.EscapeDataString(query);
        var searchUrl = $"https://www.youtube.com/results?search_query={encodedQuery}&sp=EgIQAw%3D%3D";

        var args = new List<string> { "--flat-playlist", "--dump-json", "--no-warnings" };
        AddCookieArgs(args);
        args.Add("--playlist-items");
        args.Add($"1-{count}");
        args.Add(searchUrl);

        var psi = new ProcessStartInfo
        {
            FileName = ToolPaths.YtDlp,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        psi.EnvironmentVariables["PATH"] =
            ToolPaths.ToolsDir + ";" + (Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine) ?? "") + ";" + (Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? "");

        if (File.Exists(ToolPaths.ZscalerCertPath))
        {
            psi.EnvironmentVariables["SSL_CERT_FILE"] = ToolPaths.ZscalerCertPath;
            psi.EnvironmentVariables["REQUESTS_CA_BUNDLE"] = ToolPaths.ZscalerCertPath;
        }

        foreach (var arg in args)
            psi.ArgumentList.Add(arg);

        using var process = Process.Start(psi);
        if (process is null) return results;

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var output = await outputTask;
        var stderr = await stderrTask;

        CheckForCookieError(stderr);
        CheckForNetworkInterception(stderr);

        var seenIds = new HashSet<string>();

        foreach (var line in output.Split('\n'))
        {
            if (results.Count >= count) break;

            var trimmed = line.Trim();
            if (trimmed.Length == 0) continue;

            try
            {
                using var doc = JsonDocument.Parse(trimmed);
                var root = doc.RootElement;

                var id = root.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
                if (string.IsNullOrEmpty(id)) continue;
                if (!seenIds.Add(id)) continue;

                var title = root.TryGetProperty("title", out var titleProp) ? titleProp.GetString() : null;
                if (string.IsNullOrEmpty(title)) continue;

                var channel = root.TryGetProperty("channel", out var channelProp) ? channelProp.GetString() : null;
                if (string.IsNullOrEmpty(channel))
                    channel = root.TryGetProperty("uploader", out var uploaderProp) ? uploaderProp.GetString() : null;

                string? thumbnailUrl = null;
                if (root.TryGetProperty("thumbnails", out var thumbsProp) &&
                    thumbsProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var thumb in thumbsProp.EnumerateArray())
                        if (thumb.TryGetProperty("url", out var thumbUrlProp))
                            thumbnailUrl = thumbUrlProp.GetString();
                }

                var playlistUrl = root.TryGetProperty("url", out var urlProp) &&
                                  !string.IsNullOrEmpty(urlProp.GetString())
                    ? urlProp.GetString()!
                    : $"https://www.youtube.com/playlist?list={id}";

                results.Add(new SearchResultItem(id, playlistUrl, title, channel, null,
                    thumbnailUrl, IsPlaylist: true, VideoCount: null));
            }
            catch (JsonException) { }
        }

        return results;
    }

    public async Task<int?> GetPlaylistVideoCountAsync(string playlistUrl)
    {
        var args = new List<string> { "--flat-playlist", "--dump-json", "--no-warnings" };
        AddCookieArgs(args);
        args.Add(playlistUrl);

        var psi = new ProcessStartInfo
        {
            FileName = ToolPaths.YtDlp,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        psi.EnvironmentVariables["PATH"] =
            ToolPaths.ToolsDir + ";" + (Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine) ?? "") + ";" + (Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? "");

        if (File.Exists(ToolPaths.ZscalerCertPath))
        {
            psi.EnvironmentVariables["SSL_CERT_FILE"] = ToolPaths.ZscalerCertPath;
            psi.EnvironmentVariables["REQUESTS_CA_BUNDLE"] = ToolPaths.ZscalerCertPath;
        }

        foreach (var arg in args)
            psi.ArgumentList.Add(arg);

        try
        {
            using var process = Process.Start(psi);
            if (process is null) return null;

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            var output = await outputTask;
            await stderrTask;

            var count = output
                .Split('\n')
                .Count(line => line.Trim().Length > 0 && line.TrimStart().StartsWith('{'));

            return count > 0 ? count : null;
        }
        catch { return null; }
    }

    private static readonly Regex ProgressRegex = new(
        @"\[download\]\s+(?<percent>[\d.]+)%(.*?at\s+(?<speed>\S+))?(.*?ETA\s+(?<eta>\S+))?",
        RegexOptions.Compiled);

    private static readonly Regex DestinationRegex = new(
        @"\[download\] Destination:\s+(?<path>.+)$",
        RegexOptions.Compiled);

    private void HandleOutputLine(DownloadItem item, string? line)
    {
        if (string.IsNullOrWhiteSpace(line)) return;

        var progressMatch = ProgressRegex.Match(line);
        if (progressMatch.Success)
        {
            if (double.TryParse(progressMatch.Groups["percent"].Value, CultureInfo.InvariantCulture, out var percent))
                item.ProgressPercent = percent;
            if (progressMatch.Groups["speed"].Success)
                item.Speed = progressMatch.Groups["speed"].Value;
            if (progressMatch.Groups["eta"].Success)
                item.Eta = progressMatch.Groups["eta"].Value;
            NotifyChange();
            return;
        }

        var destMatch = DestinationRegex.Match(line);
        if (destMatch.Success)
        {
            var fileName = Path.GetFileNameWithoutExtension(destMatch.Groups["path"].Value);
            item.Title = Regex.Replace(fileName, @"\.f\d+$", "");
            NotifyChange();
            return;
        }

        if (line.Contains("ERROR", StringComparison.OrdinalIgnoreCase))
        {
            item.ErrorMessage = line;
            NotifyChange();
        }
    }

    private static void CheckForCookieError(string stderr)
    {
        if (stderr.Contains("skipping cookie file entry due to invalid length", StringComparison.OrdinalIgnoreCase) ||
            stderr.Contains("invalid cookie file", StringComparison.OrdinalIgnoreCase) ||
            stderr.Contains("does not look like a Netscape format cookies file", StringComparison.OrdinalIgnoreCase))
        {
            throw new CookiesFileException(
                "Het cookies-bestand is ongeldig of corrupt.\n\n" +
                "Haal nieuwe cookies op via Instellingen \u2192 Inloggen bij websites.");
        }
    }

    private static void CheckForNetworkInterception(string stderr)
    {
        if (stderr.Contains("CERTIFICATE_VERIFY_FAILED", StringComparison.OrdinalIgnoreCase) ||
            stderr.Contains("unable to get local issuer certificate", StringComparison.OrdinalIgnoreCase) ||
            stderr.Contains("certificate verify failed", StringComparison.OrdinalIgnoreCase) ||
            stderr.Contains("self signed certificate in chain", StringComparison.OrdinalIgnoreCase) ||
            stderr.Contains("SSL: CERTIFICATE", StringComparison.OrdinalIgnoreCase))
        {
            throw new NetworkInterceptException(
                "De verbinding naar de website wordt onderschept door een SSL-inspectie proxy (mogelijk Zscaler).\n\n" +
                "yt-dlp kan het certificaat niet verifi\u00ebren. Mogelijke oplossingen:\n" +
                "\u2022 Schakel Zscaler tijdelijk uit\n" +
                "\u2022 Gebruik een cookies-bestand via Instellingen \u2192 Inloggen bij websites");
        }
    }

    private static bool LooksLikeYoutube403OrAuthIssue(string stderr, string stdout)
    {
        static bool ContainsAny(string value, params string[] needles) =>
            needles.Any(n => value.Contains(n, StringComparison.OrdinalIgnoreCase));

        return ContainsAny(stderr,
                   "HTTP Error 403",
                   "403 Forbidden",
                   "unable to download video data",
                   "cookies are needed",
                   "Please sign in",
                   "Sign in to confirm",
                   "confirm you're not a bot",
                   "This content isn't available",
                   "Requested content is not available",
                   "Login required")
               || ContainsAny(stdout,
                   "HTTP Error 403",
                   "403 Forbidden",
                   "Please sign in",
                   "Sign in to confirm",
                   "confirm you're not a bot");
    }

    /// <summary>
    /// Detecteert permanente fouten waarbij de video simpelweg niet beschikbaar is.
    /// Deze items worden stil verwijderd uit de wachtrij zonder foutmelding.
    /// </summary>
    private static bool IsPermanentError(string stderr, string stdout, string? errorMessage)
    {
        static bool ContainsAny(string? value, params string[] needles) =>
            value is not null && needles.Any(n => value.Contains(n, StringComparison.OrdinalIgnoreCase));

        var all = new[] { stderr, stdout, errorMessage };

        return all.Any(s => ContainsAny(s,
            "Video unavailable",
            "This video is private",
            "This video has been removed",
            "This video is not available",
            "who has blocked it on copyright grounds",
            "has been removed by the uploader",
            "This video is no longer available"));
    }

    private void NotifyChange() => OnChange?.Invoke();
}