using System.Drawing;
using System.IO;
using System.Windows.Forms;
using YtDlpQueueDesktop.Models;
using YtDlpQueueDesktop.Services;

namespace YtDlpQueueDesktop;

public class SettingsForm : Form
{
    private readonly TextBox _outputDirBox;
    private readonly NumericUpDown _maxConcurrentBox;

    private readonly Label _cookieStatusLabel;

    private readonly Label _ytVersionLabel;
    private readonly Label _ffmpegVersionLabel;
    private readonly Label _denoVersionLabel;

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
        ClientSize = new Size(585, 330);
        BackColor = SystemColors.Control;
        ForeColor = SystemColors.ControlText;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            ColumnCount = 3,
            RowCount = 8
        };

        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        for (var i = 0; i < 7; i++)
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        // Outputmap
        layout.Controls.Add(NewLabel("Outputmap:"), 0, 0);

        _outputDirBox = NewTextBox(Result.OutputDirectory);
        layout.Controls.Add(_outputDirBox, 1, 0);

        var browseDirButton = new Button { Text = "...", AutoSize = true };
        browseDirButton.Click += (_, _) => BrowseFolder(_outputDirBox);
        layout.Controls.Add(browseDirButton, 2, 0);

        // Max downloads
        layout.Controls.Add(NewLabel("Max. gelijktijdige downloads:"), 0, 1);

        _maxConcurrentBox = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 10,
            Value = Math.Clamp(Result.MaxConcurrentDownloads, 1, 10),
            Width = 70
        };
        layout.Controls.Add(_maxConcurrentBox, 1, 1);

        // YouTube cookies
        layout.Controls.Add(NewLabel("Verzamel YouTube cookies:"), 0, 2);

        var loginButton = new Button { Text = "Inloggen bij YouTube...", AutoSize = true };
        loginButton.Click += (_, _) => OpenYoutubeLogin();
        layout.Controls.Add(loginButton, 1, 2);

        // Cookie status
        layout.Controls.Add(NewLabel("Cookie-status:"), 0, 3);

        _cookieStatusLabel = new Label
        {
            Text = "Controleren...",
            AutoSize = true,
            MaximumSize = new Size(380, 0)
        };
        layout.Controls.Add(_cookieStatusLabel, 1, 3);

        // Separator
        var separator = new Panel
        {
            Height = 1,
            BackColor = SystemColors.ControlDark,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 10, 0, 10)
        };
        layout.SetColumnSpan(separator, 3);
        layout.Controls.Add(separator, 0, 4);

        // Tools
        var toolsPanel = new TableLayoutPanel
        {
            AutoSize = true,
            Anchor = AnchorStyles.Left
        };
        toolsPanel.ColumnCount = 3;

        // YT-DLP
        toolsPanel.Controls.Add(NewLabel("YT-DLP:"), 0, 0);
        _ytVersionLabel = NewVersionLabel();
        toolsPanel.Controls.Add(_ytVersionLabel, 1, 0);
        _updateYtDlpButton = NewUpdateButton();
        _updateYtDlpButton.Click += async (_, _) =>
            await UpdateToolAsync("yt-dlp", _updateYtDlpButton, _ytVersionLabel,
                ToolUpdater.CheckYtDlpAsync, ToolUpdater.InstallYtDlpAsync);
        toolsPanel.Controls.Add(_updateYtDlpButton, 2, 0);

        // FFMPEG
        toolsPanel.Controls.Add(NewLabel("FFMPEG:"), 0, 1);
        _ffmpegVersionLabel = NewVersionLabel();
        toolsPanel.Controls.Add(_ffmpegVersionLabel, 1, 1);
        _updateFfmpegButton = NewUpdateButton();
        _updateFfmpegButton.Click += async (_, _) =>
            await UpdateToolAsync("ffmpeg", _updateFfmpegButton, _ffmpegVersionLabel,
                ToolUpdater.CheckFfmpegAsync, ToolUpdater.InstallFfmpegAsync);
        toolsPanel.Controls.Add(_updateFfmpegButton, 2, 1);

        // DENO
        toolsPanel.Controls.Add(NewLabel("DENO:"), 0, 2);
        _denoVersionLabel = NewVersionLabel();
        toolsPanel.Controls.Add(_denoVersionLabel, 1, 2);
        _updateDenoButton = NewUpdateButton();
        _updateDenoButton.Click += async (_, _) =>
            await UpdateToolAsync("deno", _updateDenoButton, _denoVersionLabel,
                ToolUpdater.CheckDenoAsync, ToolUpdater.InstallDenoAsync);
        toolsPanel.Controls.Add(_updateDenoButton, 2, 2);

        layout.SetColumnSpan(toolsPanel, 3);
        layout.Controls.Add(toolsPanel, 0, 5);

        // Tools map pad
        var toolsDirLabel = new Label
        {
            Text = $"Tools-map: {ToolPaths.ToolsDir}",
            ForeColor = SystemColors.GrayText,
            AutoSize = true
        };
        layout.SetColumnSpan(toolsDirLabel, 3);
        layout.Controls.Add(toolsDirLabel, 0, 6);

        // Buttons
        var buttonPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Bottom,
            AutoSize = true
        };

        var cancelButton = new Button { Text = "Annuleren", DialogResult = DialogResult.Cancel };

        var okButton = new Button { Text = "Opslaan", DialogResult = DialogResult.OK };
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

    private async Task UpdateToolAsync(
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

        if (f.ShowDialog(this) == DialogResult.OK)
        {
            RefreshCookieStatus();

            MessageBox.Show(
                this,
                $"Cookies opgeslagen naar:\n{ToolPaths.CookiesPath}",
                "Inloggen gelukt",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
    }

    private void RefreshCookieStatus()
    {
        var result = CookieFileInspector.Inspect(ToolPaths.CookiesPath);
        _cookieStatusLabel.Text = $"Status: {result.Message}";
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

    private static Label NewLabel(string t) => new() { Text = t, AutoSize = true };
    private static Label NewVersionLabel() => new() { Text = "Actueel: ...", AutoSize = true };
    private static Button NewUpdateButton() => new() { Text = "Update", AutoSize = true };

    private static TextBox NewTextBox(string t) => new()
    {
        Text = t,
        Dock = DockStyle.Fill
    };

    private static void BrowseFolder(TextBox target)
    {
        using var dlg = new FolderBrowserDialog();
        if (dlg.ShowDialog() == DialogResult.OK)
            target.Text = dlg.SelectedPath;
    }
}