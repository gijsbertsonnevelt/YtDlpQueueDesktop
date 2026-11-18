using System.Drawing;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Windows.Forms;
using YtDlpQueueDesktop.Models;
using YtDlpQueueDesktop.Services;

namespace YtDlpQueueDesktop;

public class SettingsForm : Form
{
    private readonly TextBox _outputDirBox;
    private readonly NumericUpDown _maxConcurrentBox;
    private readonly Label _cookieStatusLabel;
    private readonly ToolTip _cookieToolTip = new();
    private readonly Label _ytVersionLabel;
    private readonly Label _ffmpegVersionLabel;
    private readonly Label _denoVersionLabel;
    private readonly Label _zscalerStatusLabel;
    private readonly Button _updateYtDlpButton;
    private readonly Button _updateFfmpegButton;
    private readonly Button _updateDenoButton;

    public AppSettings Result { get; private set; }

    public SettingsForm(AppSettings current)
    {
        Result = new AppSettings
        {
            OutputDirectory = current.OutputDirectory,
            MaxConcurrentDownloads = current.MaxConcurrentDownloads,
            CookiesFilePath = ToolPaths.CookiesPath
        };

        Text = "Instellingen";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        Icon = UiIcons.Settings;
        ShowIcon = true;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(560, 360);
        BackColor = SystemColors.Control;
        ForeColor = SystemColors.ControlText;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            ColumnCount = 3,
            RowCount = 8,
        };

        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        for (var i = 0; i < 7; i++)
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        // ── Outputmap ──────────────────────────────────────────────────────
        layout.Controls.Add(NewLabel("Outputmap:"), 0, 0);
        _outputDirBox = NewTextBox(Result.OutputDirectory);
        layout.Controls.Add(_outputDirBox, 1, 0);
        var browseDirButton = NewButton("...");
        browseDirButton.Click += (_, _) => BrowseFolder(_outputDirBox);
        layout.Controls.Add(browseDirButton, 2, 0);

        // ── Max downloads ──────────────────────────────────────────────────
        layout.Controls.Add(NewLabel("Max. gelijktijdige downloads:"), 0, 1);
        _maxConcurrentBox = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 10,
            Value = Math.Clamp(Result.MaxConcurrentDownloads, 1, 10),
            Width = 70,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 4, 0, 4),
        };
        layout.Controls.Add(_maxConcurrentBox, 1, 1);

        // ── Cookies (alle websites) ────────────────────────────────────────
        layout.Controls.Add(NewLabel("Cookies voor websites:"), 0, 2);
        var loginButton = NewButton("Inloggen bij websites...");
        loginButton.Click += (_, _) => OpenYoutubeLogin();
        layout.Controls.Add(loginButton, 1, 2);

        // ── Cookie status ──────────────────────────────────────────────────
        layout.Controls.Add(NewLabel("Cookie-status:"), 0, 3);
        _cookieStatusLabel = new Label
        {
            Text = "Controleren...",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 6, 0, 4),
        };
        layout.Controls.Add(_cookieStatusLabel, 1, 3);

        // ── Separator ──────────────────────────────────────────────────────
        var separator = new Panel
        {
            Height = 1,
            BackColor = SystemColors.ControlDark,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 8, 0, 8),
        };
        layout.SetColumnSpan(separator, 3);
        layout.Controls.Add(separator, 0, 4);

        // ── Tools tabel ────────────────────────────────────────────────────
        var toolsPanel = new TableLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 4,
        };
        toolsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70f));
        toolsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        toolsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        for (var i = 0; i < 4; i++)
            toolsPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32f));

        // YT-DLP
        toolsPanel.Controls.Add(NewToolLabel("YT-DLP:"), 0, 0);
        _ytVersionLabel = NewVersionLabel();
        toolsPanel.Controls.Add(_ytVersionLabel, 1, 0);
        _updateYtDlpButton = NewButton("Update");
        _updateYtDlpButton.Click += async (_, _) =>
            await UpdateToolAsync("yt-dlp", _updateYtDlpButton, _ytVersionLabel,
                ToolUpdater.CheckYtDlpAsync, ToolUpdater.InstallYtDlpAsync);
        toolsPanel.Controls.Add(_updateYtDlpButton, 2, 0);

        // FFMPEG
        toolsPanel.Controls.Add(NewToolLabel("FFMPEG:"), 0, 1);
        _ffmpegVersionLabel = NewVersionLabel();
        toolsPanel.Controls.Add(_ffmpegVersionLabel, 1, 1);
        _updateFfmpegButton = NewButton("Update");
        _updateFfmpegButton.Click += async (_, _) =>
            await UpdateToolAsync("ffmpeg", _updateFfmpegButton, _ffmpegVersionLabel,
                ToolUpdater.CheckFfmpegAsync, ToolUpdater.InstallFfmpegAsync);
        toolsPanel.Controls.Add(_updateFfmpegButton, 2, 1);

        // DENO
        toolsPanel.Controls.Add(NewToolLabel("DENO:"), 0, 2);
        _denoVersionLabel = NewVersionLabel();
        toolsPanel.Controls.Add(_denoVersionLabel, 1, 2);
        _updateDenoButton = NewButton("Update");
        _updateDenoButton.Click += async (_, _) =>
            await UpdateToolAsync("deno", _updateDenoButton, _denoVersionLabel,
                ToolUpdater.CheckDenoAsync, ToolUpdater.InstallDenoAsync);
        toolsPanel.Controls.Add(_updateDenoButton, 2, 2);

        // ZSCALER
        toolsPanel.Controls.Add(NewToolLabel("Zscaler:"), 0, 3);
        _zscalerStatusLabel = NewVersionLabel();
        _zscalerStatusLabel.Text = File.Exists(ToolPaths.ZscalerCertPath)
            ? "Certificaat aanwezig"
            : "Geen certificaat";
        _zscalerStatusLabel.ForeColor = File.Exists(ToolPaths.ZscalerCertPath)
            ? Color.DarkGreen
            : Color.Firebrick;
        toolsPanel.Controls.Add(_zscalerStatusLabel, 1, 3);
        var zscalerButton = NewButton("Cert vernieuwen");
        zscalerButton.Click += (_, _) =>
        {
            ExportZscalerCertificate();
            _zscalerStatusLabel.Text = File.Exists(ToolPaths.ZscalerCertPath)
                ? "Certificaat aanwezig"
                : "Geen certificaat";
            _zscalerStatusLabel.ForeColor = File.Exists(ToolPaths.ZscalerCertPath)
                ? Color.DarkGreen
                : Color.Firebrick;
        };
        toolsPanel.Controls.Add(zscalerButton, 2, 3);

        layout.SetColumnSpan(toolsPanel, 3);
        layout.Controls.Add(toolsPanel, 0, 5);

        // ── Buttons ────────────────────────────────────────────────────────
        var buttonPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Bottom,
            AutoSize = true,
        };

        var cancelButton = new Button { Text = "Annuleren", DialogResult = DialogResult.Cancel, AutoSize = true };
        var okButton = new Button { Text = "Opslaan", DialogResult = DialogResult.OK, AutoSize = true };
        okButton.Click += (_, _) => ApplyValues();

        var changelogButton = new Button { Text = "Changelog", AutoSize = true };
        changelogButton.Click += (_, _) =>
        {
            using var form = new ChangelogForm();
            form.ShowDialog(this);
        };

        buttonPanel.Controls.Add(cancelButton);
        buttonPanel.Controls.Add(okButton);
        buttonPanel.Controls.Add(changelogButton);

        layout.SetColumnSpan(buttonPanel, 3);
        layout.Controls.Add(buttonPanel, 0, 7);

        Controls.Add(layout);

        AcceptButton = okButton;
        CancelButton = cancelButton;

        Shown += async (_, _) =>
        {
            await LoadVersionsAsync();
            RefreshCookieStatus();
        };
    }

    private async Task LoadVersionsAsync()
    {
        await SetVersionLabelAsync(_ytVersionLabel, ToolUpdater.CheckYtDlpAsync);
        await SetVersionLabelAsync(_ffmpegVersionLabel, ToolUpdater.CheckFfmpegAsync);
        await SetVersionLabelAsync(_denoVersionLabel, ToolUpdater.CheckDenoAsync);
    }

    private static async Task SetVersionLabelAsync(Label lbl, Func<Task<UpdateCheckResult>> f)
    {
        try
        {
            var r = await f();
            lbl.Text = $"Actueel: {r.CurrentVersion}";
            lbl.ForeColor = Color.DarkGreen;
        }
        catch
        {
            lbl.Text = "Versie onbekend";
            lbl.ForeColor = Color.Firebrick;
        }
    }

    private static async Task UpdateToolAsync(
        string toolName,
        Button button,
        Label label,
        Func<Task<UpdateCheckResult>> check,
        Func<string, string, Task<string>> install)
    {
        button.Enabled = false;
        var old = button.Text;
        button.Text = "Bezig...";

        try
        {
            var r = await check();

            if (!r.IsUpdateAvailable)
            {
                label.Text = $"Actueel: {r.CurrentVersion}";
                return;
            }

            label.Text = $"Updaten naar {r.LatestVersion}...";
            await install(r.DownloadUrl, r.CurrentVersion);
            label.Text = $"Actueel: {r.LatestVersion}";
        }
        catch (Exception ex)
        {
            label.Text = "Fout";
            MessageBox.Show(ex.Message, "Update fout");
        }
        finally
        {
            button.Enabled = true;
            button.Text = old;
        }
    }

    private void OpenYoutubeLogin()
    {
        using var f = new YoutubeLoginForm();
        var result = f.ShowDialog(this);

        // Altijd verversen: "Overal uitloggen" kan het bestand ook verwijderd hebben.
        RefreshCookieStatus();

        if (result == DialogResult.OK)
        {
            var sites = f.SavedSites;
            var siteText = sites.Count == 0
                ? ""
                : $"{Environment.NewLine}{Environment.NewLine}Cookies voor {sites.Count} site(s):{Environment.NewLine}" +
                  string.Join(", ", sites.Take(25)) + (sites.Count > 25 ? ", ..." : "");

            MessageBox.Show(
                this,
                $"Cookies opgeslagen naar:{Environment.NewLine}{ToolPaths.CookiesPath}{siteText}",
                "Inloggen gelukt",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
    }

    private void ExportZscalerCertificate()
    {
        try
        {
            var store = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadOnly);

            var zscalerCerts = store.Certificates
                .Cast<X509Certificate2>()
                .Where(c => c.Subject.Contains("Zscaler", StringComparison.OrdinalIgnoreCase))
                .ToList();

            store.Close();

            if (zscalerCerts.Count == 0)
            {
                MessageBox.Show(this,
                    "Geen Zscaler certificaat gevonden in het vertrouwde root archief." +
                    Environment.NewLine + Environment.NewLine +
                    "Zorg dat het Zscaler root certificaat is geinstalleerd op dit systeem.",
                    "Certificaat niet gevonden",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            var cert = zscalerCerts.First();
            var pemPath = ToolPaths.ZscalerCertPath;
            Directory.CreateDirectory(Path.GetDirectoryName(pemPath)!);

            var b64 = Convert.ToBase64String(cert.Export(X509ContentType.Cert));
            var lines = new List<string> { "-----BEGIN CERTIFICATE-----", b64, "-----END CERTIFICATE-----" };
            File.WriteAllLines(pemPath, lines);

            MessageBox.Show(this,
                "Zscaler certificaat geexporteerd naar:" +
                Environment.NewLine + pemPath,
                "Certificaat geexporteerd",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                "Exporteren mislukt:" + Environment.NewLine + ex.Message,
                "Fout",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void RefreshCookieStatus()
    {
        var result = CookieFileInspector.Inspect(ToolPaths.CookiesPath);
        var sites = CookieFileInspector.GetSites(ToolPaths.CookiesPath);

        _cookieStatusLabel.Text = sites.Count == 0
            ? $"Status: {result.Message}"
            : $"Status: {result.Message} — {sites.Count} site(s)";

        // Volledige lijst als tooltip, de label zelf blijft kort.
        _cookieToolTip.SetToolTip(_cookieStatusLabel, sites.Count == 0 ? "" : string.Join(Environment.NewLine, sites));
    }

    private void ApplyValues()
    {
        Result = new AppSettings
        {
            OutputDirectory = _outputDirBox.Text.Trim(),
            MaxConcurrentDownloads = (int)_maxConcurrentBox.Value,
            CookiesFilePath = ToolPaths.CookiesPath
        };
    }

    private static Label NewLabel(string t) => new()
    {
        Text = t,
        AutoSize = true,
        Anchor = AnchorStyles.Left,
        Margin = new Padding(0, 6, 8, 4),
    };

    private static Label NewToolLabel(string t) => new()
    {
        Text = t,
        AutoSize = true,
        Anchor = AnchorStyles.Left | AnchorStyles.Top,
        Margin = new Padding(0, 7, 8, 0),
    };

    private static Label NewVersionLabel() => new()
    {
        Text = "Actueel: ...",
        AutoSize = true,
        Anchor = AnchorStyles.Left | AnchorStyles.Top,
        Margin = new Padding(0, 7, 8, 0),
    };

    private static Button NewButton(string t) => new()
    {
        Text = t,
        AutoSize = true,
        Anchor = AnchorStyles.Left,
        Margin = new Padding(0, 3, 4, 3),
    };

    private static TextBox NewTextBox(string text) => new()
    {
        Text = text,
        Dock = DockStyle.Fill,
        Margin = new Padding(0, 3, 8, 3),
    };

    private static void BrowseFolder(TextBox target)
    {
        using var dlg = new FolderBrowserDialog();
        if (dlg.ShowDialog() == DialogResult.OK)
            target.Text = dlg.SelectedPath;
    }
}