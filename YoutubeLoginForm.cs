using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace YtDlpQueueDesktop;

/// <summary>
/// Toont een ingebouwde browser (WebView2) waarin de gebruiker normaal bij Google/YouTube
/// inlogt. Na het klikken op "Cookies opslaan" worden de relevante cookies rechtstreeks uit de
/// browsersessie gelezen en weggeschreven naar een cookies.txt-bestand dat yt-dlp kan gebruiken.
/// </summary>
public class YoutubeLoginForm : Form
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

    private const string WebView2InstallerUrl =
        "https://go.microsoft.com/fwlink/p/?LinkId=2124703";

    private WebView2? _webView;
    private readonly Button _saveButton;
    private readonly Label _statusLabel;

    public string? SavedCookiesPath { get; private set; }

    public YoutubeLoginForm()
    {
        Text = "Inloggen bij YouTube";
        Icon = UiIcons.Youtube;
        ShowIcon = true;
        ClientSize = new Size(900, 700);
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Dpi;

        var topPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 40,
            Padding = new Padding(8),
        };

        _statusLabel = new Label
        {
            Text = "Log hieronder in bij je Google-account. Klik daarna op \"Cookies opslaan\".",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Location = new Point(8, 12),
        };

        _saveButton = new Button
        {
            Text = "Cookies opslaan",
            Anchor = AnchorStyles.Right,
            Width = 140,
            Location = new Point(topPanel.ClientSize.Width - 148, 6),
        };
        _saveButton.Click += async (_, _) => await SaveCookiesAsync();
        topPanel.Resize += (_, _) =>
        {
            _saveButton.Location = new Point(topPanel.ClientSize.Width - _saveButton.Width - 8, 6);
        };

        topPanel.Controls.Add(_statusLabel);
        topPanel.Controls.Add(_saveButton);
        Controls.Add(topPanel);

        Load += async (_, _) => await InitializeWebViewAsync();
    }

    private async Task InitializeWebViewAsync()
    {
        try
        {
            _webView = new WebView2 { Dock = DockStyle.Fill };
            Controls.Add(_webView);
            _webView.BringToFront();

            var userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "GS Software", "yt-dlp Queue", "WebView2Profile");

            Directory.CreateDirectory(userDataFolder);

            var env = await CoreWebView2Environment.CreateAsync(userDataFolder: userDataFolder);
            await _webView.EnsureCoreWebView2Async(env);
            _webView.CoreWebView2.Navigate("https://www.youtube.com");
        }
        catch (Exception ex) when (
            ex.Message.Contains("Dll was not found", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("WebView2", StringComparison.OrdinalIgnoreCase) ||
            ex is System.Runtime.InteropServices.COMException)
        {
            // WebView2 runtime ontbreekt — vraag gebruiker om te installeren
            await HandleWebView2MissingAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                $"Kan de ingebouwde browser niet starten:\n\n{ex.Message}",
                "Fout bij starten", MessageBoxButtons.OK, MessageBoxIcon.Error);
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }

    private async Task HandleWebView2MissingAsync()
    {
        var answer = MessageBox.Show(this,
            "De Microsoft Edge WebView2 Runtime is vereist voor de ingebouwde browser, " +
            "maar is niet geïnstalleerd op dit systeem.\n\n" +
            "Wil je WebView2 nu downloaden en installeren? " +
            "(ca. 2 MB, internetverbinding vereist)\n\n" +
            "Na de installatie kun je opnieuw op \"Inloggen bij YouTube\" klikken.",
            "WebView2 Runtime vereist",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (answer != DialogResult.Yes)
        {
            DialogResult = DialogResult.Cancel;
            Close();
            return;
        }

        // Download bootstrapper naar temp map
        var installerPath = Path.Combine(Path.GetTempPath(), "MicrosoftEdgeWebview2Setup.exe");

        try
        {
            _statusLabel.Text = "WebView2 downloaden...";
            _saveButton.Enabled = false;

            var bytes = await Http.GetByteArrayAsync(WebView2InstallerUrl);
            await File.WriteAllBytesAsync(installerPath, bytes);

            _statusLabel.Text = "WebView2 installeren...";

            // Draai de installer — niet silent zodat de gebruiker UAC ziet indien nodig
            var psi = new ProcessStartInfo
            {
                FileName = installerPath,
                Arguments = "/install",
                UseShellExecute = true, // nodig voor UAC prompt
            };

            using var process = Process.Start(psi);
            if (process is not null)
                await process.WaitForExitAsync();

            // Controleer of installatie geslaagd is
            if (IsWebView2Installed())
            {
                var retry = MessageBox.Show(this,
                    "WebView2 is succesvol geïnstalleerd!\n\n" +
                    "Wil je nu inloggen bij YouTube?",
                    "Installatie geslaagd",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information);

                if (retry == DialogResult.Yes)
                    await InitializeWebViewAsync();
                else
                {
                    DialogResult = DialogResult.Cancel;
                    Close();
                }
            }
            else
            {
                MessageBox.Show(this,
                    "De WebView2 installatie lijkt niet geslaagd. " +
                    "Probeer het handmatig te installeren via:\n\n" +
                    "https://developer.microsoft.com/microsoft-edge/webview2/",
                    "Installatie mislukt",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                DialogResult = DialogResult.Cancel;
                Close();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                $"Downloaden of installeren van WebView2 mislukt:\n\n{ex.Message}\n\n" +
                "Installeer het handmatig via:\n" +
                "https://developer.microsoft.com/microsoft-edge/webview2/",
                "Fout",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            DialogResult = DialogResult.Cancel;
            Close();
        }
        finally
        {
            _saveButton.Enabled = true;
            _statusLabel.Text = "Log hieronder in bij je Google-account. Klik daarna op \"Cookies opslaan\".";
            try { File.Delete(installerPath); } catch { }
        }
    }

    /// <summary>Controleert of WebView2 Runtime geïnstalleerd is via de bekende registry paden.</summary>
    private static bool IsWebView2Installed()
    {
        var keys = new[]
        {
            @"SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}",
            @"SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}",
        };

        foreach (var key in keys)
        {
            using var reg = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(key);
            if (reg?.GetValue("pv") is string version && !string.IsNullOrEmpty(version) && version != "0.0.0.0")
                return true;
        }

        using var userReg = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}");
        if (userReg?.GetValue("pv") is string userVersion && !string.IsNullOrEmpty(userVersion) && userVersion != "0.0.0.0")
            return true;

        return false;
    }

    private async Task SaveCookiesAsync()
    {
        if (_webView?.CoreWebView2 is null)
        {
            MessageBox.Show(this, "De browser is nog niet geïnitialiseerd.",
                "Fout", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _saveButton.Enabled = false;
        _saveButton.Text = "Bezig...";

        try
        {
            var cookieManager = _webView.CoreWebView2.CookieManager;
            var cookies = await cookieManager.GetCookiesAsync(null);

            var relevant = cookies
                .Where(c =>
                    c.Domain.Contains("youtube.com", StringComparison.OrdinalIgnoreCase) ||
                    c.Domain.Contains("google.com", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (relevant.Count == 0)
            {
                MessageBox.Show(this,
                    "Geen cookies gevonden voor youtube.com/google.com. Ben je daadwerkelijk ingelogd?",
                    "Geen cookies gevonden", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var path = ToolPaths.CookiesPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            WriteNetscapeCookieFile(path, relevant);

            SavedCookiesPath = path;
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                $"Opslaan van cookies mislukt: {ex.Message}",
                "Fout", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _saveButton.Enabled = true;
            _saveButton.Text = "Cookies opslaan";
        }
    }

    private static void WriteNetscapeCookieFile(string path, IEnumerable<CoreWebView2Cookie> cookies)
    {
        using var writer = new StreamWriter(path, append: false);
        writer.WriteLine("# Netscape HTTP Cookie File");
        writer.WriteLine("# Automatisch gegenereerd door yt-dlp Queue, niet handmatig bewerken.");
        writer.WriteLine();

        foreach (var cookie in cookies)
        {
            var includeSubdomains = cookie.Domain.StartsWith('.') ? "TRUE" : "FALSE";
            var secure = cookie.IsSecure ? "TRUE" : "FALSE";
            var expiry = cookie.IsSession
                ? 0L
                : new DateTimeOffset(DateTime.SpecifyKind(cookie.Expires, DateTimeKind.Utc)).ToUnixTimeSeconds();
            var cookiePath = string.IsNullOrEmpty(cookie.Path) ? "/" : cookie.Path;

            writer.WriteLine(string.Join('\t',
                cookie.Domain, includeSubdomains, cookiePath, secure, expiry, cookie.Name, cookie.Value));
        }
    }
}