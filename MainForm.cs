using System.Drawing;
using System.Windows.Forms;
using YtDlpQueueDesktop.Controls;
using YtDlpQueueDesktop.Models;
using YtDlpQueueDesktop.Services;

namespace YtDlpQueueDesktop;

public class MainForm : Form
{
    private readonly DownloadQueueService _queue;
    private AppSettings _settings;

    private TextBox _urlBox = null!;
    private ComboBox _formatBox = null!;
    private Button _addButton = null!;
    private Button _settingsButton = null!;
    private Label _workersLabel = null!;
    private DataGridView _grid = null!;
    private int _contextMenuRowIndex = -1;

    public MainForm()
    {
        _settings = SettingsStore.Load();
        _queue = new DownloadQueueService(_settings);

        // Installeer het Zscaler certificaat als dat is meegeleverd en nog niet
        // geïnstalleerd is (nodig voor WebView2 en yt-dlp op netwerken met SSL-inspectie).
        ZscalerCertInstaller.EnsureInstalled();

        BuildUi();

        var (toolsOk, missing) = ToolPaths.Check();
        if (!toolsOk)
        {
            MessageBox.Show(
                $"De volgende bestanden ontbreken in de tools-map:\n\n" +
                string.Join("\n", missing.Select(m => $"  • {m}")) +
                $"\n\nVerwacht in:\n  {ToolPaths.ToolsDir}\n\n" +
                "Downloads zullen mislukken totdat deze bestanden aanwezig zijn.",
                "Ontbrekende tools",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

        _queue.OnChange += () =>
        {
            try
            {
                if (IsHandleCreated && !IsDisposed)
                    BeginInvoke(new MethodInvoker(RefreshGrid));
            }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }
        };

        _queue.Start();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _queue.Shutdown();
        base.OnFormClosing(e);
    }

    // ---------- UI opbouw ----------

    private void BuildUi()
    {
        Text = "yt-dlp Queue";
        Icon = UiIcons.Main;
        ClientSize = new Size(1000, 600);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = SystemColors.Control;
        ForeColor = SystemColors.ControlText;
        MinimumSize = new Size(760, 400);

        var header = BuildHeader();
        var inputRow = BuildInputRow();
        _grid = BuildGrid();

        Controls.Add(_grid);
        Controls.Add(inputRow);
        Controls.Add(header);
    }

    private Control BuildHeader()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 46,
            Padding = new Padding(12, 0, 12, 0),
            BackColor = SystemColors.Control,
        };

        var title = new Label
        {
            Text = "yt-dlp Queue",
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            ForeColor = SystemColors.ControlText,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Location = new Point(12, 12),
        };

        var helpButton = new Button
        {
            Text = "?",
            Width = 28,
            Height = 28,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand,
        };
        helpButton.FlatAppearance.BorderSize = 1;
        helpButton.Click += (_, _) =>
        {
            using var form = new HelpForm();
            form.ShowDialog(this);
        };

        var aboutButton = new Button
        {
            Text = "♥",
            Width = 28,
            Height = 28,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            ForeColor = Color.Firebrick,
        };
        aboutButton.FlatAppearance.BorderSize = 1;
        aboutButton.Click += (_, _) =>
        {
            using var form = new AboutForm();
            form.ShowDialog(this);
        };

        _workersLabel = new Label
        {
            Text = "Workers: 0/0",
            ForeColor = SystemColors.GrayText,
            AutoSize = true,
            Anchor = AnchorStyles.Right,
        };

        panel.Resize += (_, _) =>
        {
            _workersLabel.Location = new Point(panel.Width - _workersLabel.Width - 12, 16);
            helpButton.Location = new Point(panel.Width - _workersLabel.Width - helpButton.Width - 20, 9);
            aboutButton.Location = new Point(panel.Width - _workersLabel.Width - helpButton.Width - aboutButton.Width - 28, 9);
        };

        var rule = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 1,
            BackColor = SystemColors.ControlDark,
        };

        panel.Controls.Add(title);
        panel.Controls.Add(aboutButton);
        panel.Controls.Add(helpButton);
        panel.Controls.Add(_workersLabel);
        panel.Controls.Add(rule);
        return panel;
    }

    private Control BuildInputRow()
    {
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 48,
            ColumnCount = 4,
            Padding = new Padding(12, 8, 12, 8),
            BackColor = SystemColors.Control,
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130f));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100f));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100f));
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        _urlBox = new TextBox
        {
            Dock = DockStyle.Fill,
            PlaceholderText = "URL, zoekterm, zoekterm! voor playlists, of sc: zoekterm voor andere sites — Enter...",
            Margin = new Padding(0, 3, 8, 3),
        };
        _urlBox.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                AddToQueue();
            }
        };

        _formatBox = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Margin = new Padding(0, 3, 8, 3),
        };
        _formatBox.Items.AddRange(["Video (beste kwaliteit)", "Video (MP4)", "Audio (mp3)"]);
        _formatBox.SelectedIndex = 1;

        _addButton = new Button
        {
            Text = "Toevoegen",
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 3, 8, 3),
            UseVisualStyleBackColor = true,
        };
        _addButton.Click += (_, _) => AddToQueue();

        _settingsButton = new Button
        {
            Text = "Instellingen",
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 3, 0, 3),
            UseVisualStyleBackColor = true,
        };
        _settingsButton.Click += (_, _) => OpenSettings();

        table.Controls.Add(_urlBox, 0, 0);
        table.Controls.Add(_formatBox, 1, 0);
        table.Controls.Add(_addButton, 2, 0);
        table.Controls.Add(_settingsButton, 3, 0);

        return table;
    }

    private DataGridView BuildGrid()
    {
        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            ReadOnly = false,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
            RowTemplate = new DataGridViewRow { Height = 28 },
            Margin = new Padding(12, 0, 12, 12),
            ShowCellToolTips = true,
            BackgroundColor = SystemColors.Window,
        };

        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Title", HeaderText = "Titel / URL", FillWeight = 20 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Format", HeaderText = "Type", FillWeight = 8 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "Status", FillWeight = 30 });
        grid.Columns.Add(new DataGridViewProgressColumn { Name = "Progress", HeaderText = "Voortgang", FillWeight = 14 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Speed", HeaderText = "Snelheid", FillWeight = 10 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Eta", HeaderText = "ETA", FillWeight = 6 });
        grid.Columns.Add(new DataGridViewButtonColumn
        {
            Name = "Action",
            HeaderText = "",
            Text = "Annuleren",
            UseColumnTextForButtonValue = false,
            FillWeight = 10,
        });

        foreach (DataGridViewColumn col in grid.Columns)
        {
            col.SortMode = DataGridViewColumnSortMode.NotSortable;
            col.ReadOnly = col.Name != "Action";
        }

        var copyErrorMenuItem = new ToolStripMenuItem("Foutmelding kopiëren naar klembord");
        copyErrorMenuItem.Click += (_, _) => CopyErrorToClipboard();

        var removeMenuItem = new ToolStripMenuItem("Verwijderen uit wachtrij");
        removeMenuItem.Click += (_, _) => RemoveContextRow();

        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add(copyErrorMenuItem);
        contextMenu.Items.Add(removeMenuItem);
        contextMenu.Opening += (_, e) =>
        {
            var item = GetItemForRow(_contextMenuRowIndex);
            if (item is null) { e.Cancel = true; return; }

            copyErrorMenuItem.Available = !string.IsNullOrEmpty(item.ErrorMessage);
            removeMenuItem.Available = item.Status != DownloadStatus.Bezig;

            if (!copyErrorMenuItem.Available && !removeMenuItem.Available)
                e.Cancel = true;
        };
        grid.ContextMenuStrip = contextMenu;

        grid.CellMouseDown += (_, e) =>
        {
            if (e.Button != MouseButtons.Right) return;
            _contextMenuRowIndex = e.RowIndex;
            if (e.RowIndex >= 0)
            {
                grid.ClearSelection();
                grid.Rows[e.RowIndex].Selected = true;
            }
        };

        grid.CellClick += Grid_CellClick;
        return grid;
    }

    private DownloadItem? GetItemForRow(int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= _grid.Rows.Count) return null;
        if (_grid.Rows[rowIndex].Tag is not Guid id) return null;
        return _queue.Items.FirstOrDefault(i => i.Id == id);
    }

    private void CopyErrorToClipboard()
    {
        var item = GetItemForRow(_contextMenuRowIndex);
        if (!string.IsNullOrEmpty(item?.ErrorMessage))
            Clipboard.SetText(item.ErrorMessage);
    }

    private void RemoveContextRow()
    {
        var item = GetItemForRow(_contextMenuRowIndex);
        if (item is not null)
            _queue.Remove(item.Id);
    }

    // ---------- Acties ----------

    private async void AddToQueue()
    {
        var input = _urlBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(input)) return;

        var format = _formatBox.SelectedIndex switch
        {
            1 => "mp4",
            2 => "audio",
            _ => "video",
        };

        _urlBox.Clear();
        _urlBox.Focus();

        if (!LooksLikeUrl(input))
        {
            // Voorvoegsel zoals "sc:" kiest de zoeksite; zonder voorvoegsel YouTube.
            var (provider, query) = SearchProviders.Parse(input);
            if (query.Length == 0) return;

            // "!" aan het eind = playlists zoeken, alleen ondersteund op YouTube.
            var isPlaylistSearch = provider.SupportsPlaylists && query.EndsWith("!");
            if (isPlaylistSearch)
                query = query[..^1].TrimEnd();

            var searchForm = new SearchResultsForm(_queue, query, format, isPlaylistSearch, provider);
            searchForm.Show(this);
            return;
        }

        ExpandResult result;
        try
        {
            result = await _queue.ExpandUrlAsync(input);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Kan URL niet uitlezen: {ex.Message}", "Fout",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        if (result.Urls.Count > 50)
        {
            var confirm = MessageBox.Show(this,
                $"Deze playlist bevat {result.Urls.Count} video's. Wil je ze allemaal toevoegen aan de wachtrij?",
                "Grote playlist", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes) return;
        }

        foreach (var videoUrl in result.Urls)
            _queue.Enqueue(videoUrl, format, result.PlaylistFolder);
    }

    private static bool LooksLikeUrl(string text) =>
        Uri.TryCreate(text, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private void OpenSettings()
    {
        using var dlg = new SettingsForm(_settings);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        var concurrencyChanged = dlg.Result.MaxConcurrentDownloads != _settings.MaxConcurrentDownloads;

        _settings = dlg.Result;
        SettingsStore.Save(_settings);
        _queue.UpdateSettings(_settings);

        if (concurrencyChanged)
        {
            MessageBox.Show(this,
                "Het aantal gelijktijdige downloads wordt actief na het herstarten van de app.",
                "Instellingen opgeslagen", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void Grid_CellClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
        if (_grid.Columns[e.ColumnIndex].Name != "Action") return;
        if (_grid.Rows[e.RowIndex].Tag is not Guid id) return;

        var item = _queue.Items.FirstOrDefault(i => i.Id == id);
        if (item is null) return;

        switch (item.Status)
        {
            case DownloadStatus.Wachtend:
            case DownloadStatus.Bezig:
                _queue.Cancel(id);
                break;
            case DownloadStatus.Mislukt:
            case DownloadStatus.Geannuleerd:
                _queue.Retry(id);
                break;
            default:
                _queue.Remove(id);
                break;
        }
    }

    // ---------- Live verversen ----------

    private void RefreshGrid()
    {
        var items = _queue.Items;
        var existingRows = _grid.Rows.Cast<DataGridViewRow>()
            .Where(r => r.Tag is Guid)
            .ToDictionary(r => (Guid)r.Tag!, r => r);

        foreach (var (id, row) in existingRows)
            if (!items.Any(i => i.Id == id))
                _grid.Rows.Remove(row);

        foreach (var item in items)
        {
            DataGridViewRow row;
            if (existingRows.TryGetValue(item.Id, out var existing))
            {
                row = existing;
            }
            else
            {
                var idx = _grid.Rows.Add();
                row = _grid.Rows[idx];
                row.Tag = item.Id;
            }

            row.Cells["Title"].Value = item.Title ?? item.Url;
            row.Cells["Format"].Value = item.Format == "audio" ? "Audio" :
                                        item.Format == "mp4" ? "MP4" : "Video";
            row.Cells["Status"].Value = item.Status == DownloadStatus.Mislukt &&
                                        !string.IsNullOrEmpty(item.ErrorMessage)
                ? $"Mislukt: {item.ErrorMessage}"
                : item.Status == DownloadStatus.Bezig && !string.IsNullOrEmpty(item.ErrorMessage)
                ? item.ErrorMessage
                : item.Status.ToString();
            row.Cells["Status"].Style.ForeColor = StatusColor(item.Status);
            row.Cells["Status"].ToolTipText = item.ErrorMessage ?? "";
            row.Cells["Title"].ToolTipText = item.Url;
            row.Cells["Progress"].Value = item.ProgressPercent;
            row.Cells["Speed"].Value = item.Speed ?? "";
            row.Cells["Eta"].Value = string.IsNullOrEmpty(item.Eta) ? "" : $"ETA {item.Eta}";

            row.Cells["Action"].Value = item.Status switch
            {
                DownloadStatus.Wachtend or DownloadStatus.Bezig => "Annuleren",
                DownloadStatus.Mislukt or DownloadStatus.Geannuleerd => "Opnieuw proberen",
                _ => "Verwijderen",
            };
        }

        _workersLabel.Text = $"Workers: {_queue.ActiveCount}/{_queue.MaxConcurrent}";
    }

    private static Color StatusColor(DownloadStatus status) => status switch
    {
        DownloadStatus.Bezig => Color.Blue,
        DownloadStatus.Voltooid => Color.Green,
        DownloadStatus.Mislukt => Color.Red,
        DownloadStatus.Geannuleerd => Color.DarkOrange,
        _ => SystemColors.GrayText,
    };
}