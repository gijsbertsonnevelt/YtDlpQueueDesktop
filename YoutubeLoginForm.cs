using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace YtDlpQueueDesktop;

/// <summary>
/// Ingebouwde browser (WebView2) waarin de gebruiker bij willekeurige websites inlogt
/// (YouTube, Facebook, Instagram, X, TikTok, enz.). De browser gebruikt een
/// vast profiel, dus logins blijven bewaard tussen sessies. Bij "Cookies opslaan" worden
/// ALLE cookies uit dat profiel weggeschreven naar één Netscape cookies.txt. yt-dlp stuurt
/// per verzoek alleen de cookies mee die bij het domein horen, dus één bestand voor alle
/// sites werkt prima.
/// (De klassenaam is historisch; hij is behouden zodat het .resx-bestand gekoppeld blijft.)
/// </summary>
public class YoutubeLoginForm : Form
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

    private const string WebView2InstallerUrl =
        "https://go.microsoft.com/fwlink/p/?LinkId=2124703";

    private const string DefaultStatusText =
        "Ga naar een website, log in en klik daarna op \"Cookies opslaan\".";

    /// <summary>Snelkeuzes in de adresbalk. Elke andere URL kan ook ingetypt worden.</summary>
    private static readonly string[] PresetSites =
    [
        "https://www.youtube.com",
        "https://www.facebook.com",
        "https://www.instagram.com",
        "https://x.com",
        "https://www.tiktok.com",
        "https://vimeo.com",
        "https://www.reddit.com",
    ];

    private static readonly char[] InvalidCookieChars = ['\t', '\r', '\n'];

    private WebView2? _webView;
    private readonly ComboBox _addressBox;
    private readonly Button _goButton;
    private readonly Button _saveButton;
    private readonly Button _clearButton;
    private readonly Label _statusLabel;
    private readonly string? _startUrl;

    public string? SavedCookiesPath { get; private set; }

    /// <summary>Sites (basisdomeinen) waarvoor cookies zijn opgeslagen.</summary>
    public IReadOnlyList<string> SavedSites { get; private set; } = [];

    /// <param name="startUrl">Optionele pagina om mee te openen; standaard YouTube.</param>
    public YoutubeLoginForm(string? startUrl = null)
    {
        _startUrl = startUrl;

        Text = "Inloggen bij websites";
        Icon = UiIcons.Youtube;
        ShowIcon = true;
        ClientSize = new Size(1000, 760);
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Dpi;

        var topPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 4,
            RowCount = 2,
            Padding = new Padding(8, 6, 8, 6),
        };
        topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        // ── Rij 1: adresbalk ───────────────────────────────────────────────
        _addressBox = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDown,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 2, 6, 2),
        };
        _addressBox.Items.AddRange(PresetSites);
        _addressBox.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                NavigateToAddress();
            }
        };
        _addressBox.SelectionChangeCommitted += (_, _) =>
            BeginInvoke(NavigateToAddress);

        _goButton = new Button
        {
            Text = "Ga",
            AutoSize = true,
            Margin = new Padding(0, 1, 6, 1),
        };
        _goButton.Click += (_, _) => NavigateToAddress();

        _saveButton = new Button
        {
            Text = "Cookies opslaan",
            AutoSize = true,
            Margin = new Padding(0, 1, 6, 1),
        };
        _saveButton.Click += async (_, _) => await SaveCookiesAsync();

        _clearButton = new Button
        {
            Text = "Overal uitloggen",
            AutoSize = true,
            Margin = new Padding(0, 1, 0, 1),
        };
        _clearButton.Click += (_, _) => ClearAllCookies();

        topPanel.Controls.Add(_addressBox, 0, 0);
        topPanel.Controls.Add(_goButton, 1, 0);
        topPanel.Controls.Add(_saveButton, 2, 0);
        topPanel.Controls.Add(_clearButton, 3, 0);

        // ── Rij 2: status ──────────────────────────────────────────────────
        _statusLabel = new Label
        {
            Text = DefaultStatusText,
            AutoSize = true,
            Margin = new Padding(0, 6, 0, 2),
        };
        topPanel.Controls.Add(_statusLabel, 0, 1);
        topPanel.SetColumnSpan(_statusLabel, 4);

        Controls.Add(topPanel);

        Load += async (_, _) => await InitializeWebViewAsync();
    }

    /// <summary>Navigeert naar wat er in de adresbalk staat. Zonder schema wordt https:// gebruikt.</summary>
    private void NavigateToAddress()
    {
        if (_webView?.CoreWebView2 is null) return;

        var text = _addressBox.Text.Trim();
        if (text.Length == 0) return;

        if (!text.Contains("://", StringComparison.Ordinal))
            text = "https://" + text;

        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            _statusLabel.Text = "Ongeldig adres.";
            return;
        }

        _webView.CoreWebView2.Navigate(uri.AbsoluteUri);
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

            // Configureer WebView2 omgeving — voeg Zscaler certificaat toe als dat aanwezig is
            var options = new CoreWebView2EnvironmentOptions();

            if (File.Exists(ToolPaths.ZscalerCertPath))
            {
                // Laad het PEM bestand en geef het mee aan WebView2
                // zodat het Zscaler certificaat wordt vertrouwd in de ingebouwde browser
                options.AdditionalBrowserArguments =
                    $"--ignore-certificate-errors-spki-list=" +
                    GetCertificateSpki(ToolPaths.ZscalerCertPath);
            }

            var env = await CoreWebView2Environment.CreateAsync(
                browserExecutableFolder: null,
                userDataFolder: userDataFolder,
                options: options);

            await _webView.EnsureCoreWebView2Async(env);

            // Adresbalk bijwerken als de gebruiker binnen de pagina navigeert,
            // behalve terwijl hij zelf aan het typen is.
            _webView.CoreWebView2.SourceChanged += (_, _) =>
            {
                if (!_addressBox.Focused)
                    _addressBox.Text = _webView.CoreWebView2.Source;
            };

            // Pop-ups (bijv. "Inloggen met Google" op andere sites) opent WebView2 standaard in een
            // eigen venster met hetzelfde profiel, dus die cookies komen vanzelf in dit profiel.

            var start = string.IsNullOrWhiteSpace(_startUrl) ? PresetSites[0] : _startUrl!;
            _addressBox.Text = start;
            _webView.CoreWebView2.Navigate(start);
        }
        catch (Exception ex) when (
            ex.Message.Contains("Dll was not found", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("WebView2", StringComparison.OrdinalIgnoreCase) ||
            ex is System.Runtime.InteropServices.COMException)
        {
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

    /// <summary>Haal de SPKI hash op uit een PEM certificaat bestand voor gebruik
    /// als --ignore-certificate-errors-spki-list argument.</summary>
    private static string GetCertificateSpki(string pemPath)
    {
        try
        {
            var pem = File.ReadAllText(pemPath);
            var b64 = pem
                .Replace("-----BEGIN CERTIFICATE-----", "")
                .Replace("-----END CERTIFICATE-----", "")
                .Replace("\r", "").Replace("\n", "").Trim();
            var certBytes = Convert.FromBase64String(b64);
            using var cert = System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadCertificate(certBytes);
            var spkiBytes = cert.PublicKey.ExportSubjectPublicKeyInfo();
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hash = sha256.ComputeHash(spkiBytes);
            return Convert.ToBase64String(hash);
        }
        catch
        {
            return string.Empty;
        }
    }

    private async Task HandleWebView2MissingAsync()
    {
        var answer = MessageBox.Show(this,
            "De Microsoft Edge WebView2 Runtime is vereist voor de ingebouwde browser, " +
            "maar is niet geïnstalleerd op dit systeem.\n\n" +
            "Wil je WebView2 nu downloaden en installeren? " +
            "(ca. 2 MB, internetverbinding vereist)\n\n" +
            "Na de installatie kun je opnieuw op \"Inloggen bij websites\" klikken.",
            "WebView2 Runtime vereist",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (answer != DialogResult.Yes)
        {
            DialogResult = DialogResult.Cancel;
            Close();
            return;
        }

        var installerPath = Path.Combine(Path.GetTempPath(), "MicrosoftEdgeWebview2Setup.exe");

        try
        {
            _statusLabel.Text = "WebView2 downloaden...";
            _saveButton.Enabled = false;

            var bytes = await Http.GetByteArrayAsync(WebView2InstallerUrl);
            await File.WriteAllBytesAsync(installerPath, bytes);

            _statusLabel.Text = "WebView2 installeren...";

            var psi = new ProcessStartInfo
            {
                FileName = installerPath,
                Arguments = "/install",
                UseShellExecute = true,
            };

            using var process = Process.Start(psi);
            if (process is not null)
                await process.WaitForExitAsync();

            if (IsWebView2Installed())
            {
                var retry = MessageBox.Show(this,
                    "WebView2 is succesvol geïnstalleerd!\n\n" +
                    "Wil je nu inloggen?",
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
            _statusLabel.Text = DefaultStatusText;
            try { File.Delete(installerPath); } catch { }
        }
    }

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
            // null = alle cookies uit dit WebView2-profiel, van alle sites waar ooit is ingelogd.
            var cookieManager = _webView.CoreWebView2.CookieManager;
            var cookies = await cookieManager.GetCookiesAsync(null);

            if (cookies.Count == 0)
            {
                MessageBox.Show(this,
                    "Er zijn geen cookies gevonden. Ben je daadwerkelijk ingelogd op een website?",
                    "Geen cookies gevonden", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var path = ToolPaths.CookiesPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            WriteNetscapeCookieFile(path, cookies);

            SavedCookiesPath = path;
            SavedSites = Services.CookieFileInspector.GetSites(path);
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

    /// <summary>Wist alle cookies uit het browserprofiel én het cookies.txt-bestand.</summary>
    private void ClearAllCookies()
    {
        var answer = MessageBox.Show(this,
            "Hiermee log je in de ingebouwde browser bij alle websites uit en wordt het " +
            "opgeslagen cookies-bestand verwijderd.\n\nDoorgaan?",
            "Overal uitloggen",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (answer != DialogResult.Yes) return;

        try
        {
            _webView?.CoreWebView2?.CookieManager.DeleteAllCookies();

            if (File.Exists(ToolPaths.CookiesPath))
                File.Delete(ToolPaths.CookiesPath);

            SavedCookiesPath = null;
            SavedSites = [];
            _statusLabel.Text = "Uitgelogd en cookies verwijderd. " + DefaultStatusText;
            _webView?.CoreWebView2?.Reload();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                $"Wissen van cookies mislukt: {ex.Message}",
                "Fout", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            // Tabs of regeleindes in een waarde zouden het bestandsformaat breken.
            if (cookie.Value.IndexOfAny(InvalidCookieChars) >= 0 ||
                cookie.Name.IndexOfAny(InvalidCookieChars) >= 0)
                continue;

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
