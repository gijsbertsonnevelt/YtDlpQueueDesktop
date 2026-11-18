using System.Drawing;
using System.Net.Http;
using System.Windows.Forms;
using YtDlpQueueDesktop.Services;

namespace YtDlpQueueDesktop;

/// <summary>
/// Toont zoekresultaten (video's of playlists) met thumbnail, titel, kanaal en duur/aantal.
/// Blijft als los, niet-modaal venster open zodat je meerdere items kunt toevoegen.
/// Standaard wordt op YouTube gezocht; met een voorvoegsel (bijv. "sc:") op een andere site,
/// zie <see cref="SearchProviders"/>. Playlist-zoekmodus ("!" aan het eind) is alleen voor YouTube.
/// </summary>
public class SearchResultsForm : Form
{
    private static readonly HttpClient Http = CreateHttpClient();

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0 Safari/537.36");
        return client;
    }

    private readonly DownloadQueueService _queue;
    private readonly string _query;
    private readonly string _format;
    private readonly string _folderName;
    private readonly bool _isPlaylistSearch;
    private readonly SearchProvider _provider;
    private readonly FlowLayoutPanel _resultsPanel;
    private readonly Label _statusLabel;
    private readonly ToolTip _toolTip = new();

    public SearchResultsForm(DownloadQueueService queue, string query, string format,
        bool isPlaylistSearch = false, SearchProvider? provider = null)
    {
        _queue = queue;
        _query = query;
        _format = format;
        _provider = provider ?? SearchProviders.YouTube;
        _isPlaylistSearch = isPlaylistSearch && _provider.SupportsPlaylists;
        _folderName = DownloadQueueService.SanitizeFolderName(query);

        var siteSuffix = _provider.IsYoutube ? "" : $" ({_provider.Name})";
        Text = _isPlaylistSearch ? $"Playlists: {query}" : $"Zoeken: {query}{siteSuffix}";
        ClientSize = new Size(640, 560);
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = SystemColors.Control;
        ForeColor = SystemColors.ControlText;
        MinimumSize = new Size(480, 360);

        _statusLabel = new Label
        {
            Text = _isPlaylistSearch
                ? $"Bezig met zoeken naar playlists voor \"{query}\"..."
                : $"Bezig met zoeken naar \"{query}\" op {_provider.Name}...",
            Dock = DockStyle.Top,
            Height = 28,
            Padding = new Padding(12, 6, 12, 0),
            ForeColor = SystemColors.GrayText,
        };

        var closeButton = new Button
        {
            Text = "Sluiten",
            Dock = DockStyle.Bottom,
            Height = 36,
        };
        closeButton.Click += (_, _) => Close();

        var scrollHost = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = SystemColors.Window,
        };

        _resultsPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            Dock = DockStyle.Top,
            Padding = new Padding(8),
        };
        scrollHost.Controls.Add(_resultsPanel);

        Controls.Add(scrollHost);
        Controls.Add(closeButton);
        Controls.Add(_statusLabel);

        Load += async (_, _) =>
        {
            try
            {
                await LoadResultsAsync();
            }
            catch (CookiesFileException ex)
            {
                if (!IsDisposed)
                {
                    _statusLabel.Text = "Zoeken mislukt: cookies-bestand ongeldig.";
                    MessageBox.Show(
                        ex.Message,
                        "Cookies-bestand vernieuwen",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
            }
            catch (NetworkInterceptException ex)
            {
                if (!IsDisposed)
                {
                    _statusLabel.Text = "Zoeken mislukt: netwerkverbinding geblokkeerd.";
                    MessageBox.Show(
                        ex.Message,
                        "Netwerkverbinding geblokkeerd",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                if (!IsDisposed)
                    _statusLabel.Text = $"Zoeken mislukt: {ex.Message}";
            }
        };
    }

    private async Task LoadResultsAsync()
    {
        // Geen try/catch hier — exceptions stromen door naar de Load handler
        var results = _isPlaylistSearch
            ? await _queue.SearchPlaylistsAsync(_query)
            : await _queue.SearchAsync(_provider, _query);

        if (IsDisposed) return;

        if (results.Count == 0)
        {
            _statusLabel.Text = _isPlaylistSearch
                ? $"Geen playlists gevonden voor \"{_query}\"."
                : $"Geen resultaten gevonden voor \"{_query}\" op {_provider.Name}.";
            return;
        }

        _statusLabel.Text = _isPlaylistSearch
            ? $"{results.Count} playlists voor \"{_query}\":"
            : $"{results.Count} resultaten voor \"{_query}\" op {_provider.Name}:";

        foreach (var result in results)
        {
            var (row, subtitleLabel) = BuildResultRow(result);
            _resultsPanel.Controls.Add(row);

            if (result.IsPlaylist)
                _ = LoadVideoCountAsync(result.Url, result.Channel, subtitleLabel);
        }
    }

    /// <summary>Haalt het aantal video's op voor een playlist en update de subtitelLabel zodra
    /// de count bekend is. Draait op een achtergrondthread; marshal terug naar UI-thread.</summary>
    private async Task LoadVideoCountAsync(string playlistUrl, string? channel, Label subtitleLabel)
    {
        var count = await _queue.GetPlaylistVideoCountAsync(playlistUrl);

        if (subtitleLabel.IsDisposed) return;

        subtitleLabel.Invoke(() =>
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(channel)) parts.Add(channel);
            parts.Add(count.HasValue ? $"{count} video's" : "? video's");
            subtitleLabel.Text = string.Join(" • ", parts);
        });
    }

    private (Control row, Label subtitleLabel) BuildResultRow(SearchResultItem result)
    {
        var row = new Panel
        {
            Width = 580,
            Height = 88,
            Margin = new Padding(0, 0, 0, 4),
        };

        var thumb = new PictureBox
        {
            Size = new Size(120, 68),
            Location = new Point(4, 10),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = SystemColors.ControlLight,
        };

        if (result.IsPlaylist)
            _toolTip.SetToolTip(thumb, "Playlist");

        row.Controls.Add(thumb);

        var titleLabel = new Label
        {
            Text = result.Title,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Location = new Point(134, 8),
            Size = new Size(340, 40),
            AutoEllipsis = true,
        };
        row.Controls.Add(titleLabel);

        // Initiële subtitel: kanaal tonen, videocount komt asynchroon binnen
        var subtitleParts = new List<string>();
        if (!string.IsNullOrEmpty(result.Channel)) subtitleParts.Add(result.Channel);
        if (result.IsPlaylist)
            subtitleParts.Add("video's laden...");
        else if (!string.IsNullOrEmpty(result.Duration))
            subtitleParts.Add(result.Duration);

        var subtitleLabel = new Label
        {
            Text = string.Join(" • ", subtitleParts),
            ForeColor = SystemColors.GrayText,
            Location = new Point(134, 50),
            Size = new Size(340, 20),
            AutoEllipsis = true,
        };
        row.Controls.Add(subtitleLabel);

        var addButton = new Button
        {
            Text = "Toevoegen",
            Location = new Point(484, 28),
            Width = 90,
            Height = 30,
        };

        if (result.IsPlaylist)
        {
            addButton.Click += async (_, _) =>
            {
                addButton.Enabled = false;
                addButton.Text = "Bezig...";
                try
                {
                    var expanded = await _queue.ExpandUrlAsync(result.Url);
                    var folder = !string.IsNullOrEmpty(expanded.PlaylistFolder)
                        ? Path.Combine(_folderName, expanded.PlaylistFolder)
                        : _folderName;
                    foreach (var url in expanded.Urls)
                        _queue.Enqueue(url, _format, folder);
                    addButton.Text = $"+{expanded.Urls.Count} video's";
                }
                catch
                {
                    addButton.Text = "Mislukt";
                    addButton.Enabled = true;
                }
            };
        }
        else
        {
            addButton.Click += (_, _) =>
            {
                _queue.Enqueue(result.Url, _format, _folderName);
                addButton.Text = "Toegevoegd";
                addButton.Enabled = false;
            };
        }

        row.Controls.Add(addButton);

        if (!string.IsNullOrEmpty(result.ThumbnailUrl))
            _ = LoadThumbnailAsync(thumb, result.ThumbnailUrl);

        return (row, subtitleLabel);
    }

    private async Task LoadThumbnailAsync(PictureBox box, string url)
    {
        try
        {
            var bytes = await Http.GetByteArrayAsync(url);
            var stream = new MemoryStream(bytes);
            var image = Image.FromStream(stream);
            if (box.IsDisposed) return;
            box.Image = image;
        }
        catch (Exception ex)
        {
            if (!box.IsDisposed)
                _toolTip.SetToolTip(box, $"Kon thumbnail niet laden:\n{ex.GetType().Name}: {ex.Message}");
        }
    }
}