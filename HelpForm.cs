using System.Drawing;
using System.Windows.Forms;

namespace YtDlpQueueDesktop;

/// <summary>
/// Toont gebruiksuitleg en probleemoplossing voor yt-dlp Queue.
/// </summary>
public class HelpForm : Form
{
    public HelpForm()
    {
        Text = "Help — yt-dlp Queue";
        ClientSize = new Size(620, 580);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowIcon = false;
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = SystemColors.Window;

        var tabControl = new TabControl
        {
            Dock = DockStyle.Fill,
            Padding = new Point(12, 4),
        };

        tabControl.TabPages.Add(BuildGebruikTab());
        tabControl.TabPages.Add(BuildProbleemoplossingTab());

        var closeButton = new Button
        {
            Text = "Sluiten",
            Dock = DockStyle.Bottom,
            Height = 36,
        };
        closeButton.Click += (_, _) => Close();

        var separator = new Panel
        {
            Height = 1,
            Dock = DockStyle.Bottom,
            BackColor = SystemColors.ControlDark,
        };

        Controls.Add(tabControl);
        Controls.Add(separator);
        Controls.Add(closeButton);
    }

    private static TabPage BuildGebruikTab()
    {
        var page = new TabPage("Gebruik");
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };

        var sections = new (string Title, (string Label, string Text)[] Items)[]
        {
            ("Video's downloaden", [
                ("URL invoeren",
                    "Plak een URL van YouTube of een andere site (Facebook, Vimeo, enz.) in het invoerveld en druk op Enter of klik op Toevoegen. " +
                    "De video wordt direct toegevoegd aan de wachtrij."),
                ("Formaat kiezen",
                    "Kies het gewenste formaat in de dropdown:\n" +
                    "• Video (beste kwaliteit) — downloadt in de beste beschikbare kwaliteit\n" +
                    "• Video (MP4) — downloadt als MP4, compatibel met Windows Media Player\n" +
                    "• Audio (mp3) — downloadt alleen het geluid als MP3-bestand"),
                ("Playlist via URL",
                    "Plak een YouTube playlist URL. De app vraagt of je alle video's wilt toevoegen " +
                    "als de playlist groter is dan 50 video's. De video's worden opgeslagen in een " +
                    "submap met de naam van de playlist."),
            ]),
            ("Zoeken", [
                ("Video zoeken",
                    "Typ een zoekterm (zonder uitroepteken) en druk op Enter. " +
                    "Er verschijnt een zoekvenster met 12 resultaten inclusief thumbnail, " +
                    "kanaal en duur. Klik op Toevoegen om een video aan de wachtrij toe te voegen."),
                ("Playlists zoeken",
                    "Typ een zoekterm gevolgd door een uitroepteken (!) en druk op Enter. " +
                    "Bijvoorbeeld: Bassie en Adriaan!\n" +
                    "Er verschijnt een zoekvenster met playlists. Het aantal video's per " +
                    "playlist wordt asynchroon opgehaald en verschijnt vanzelf."),
                ("Zoeken op andere sites",
                    "Zet een voorvoegsel voor de zoekterm om op een andere site te zoeken:\n" +
                    "• sc: — SoundCloud (bijv. sc: lofi beats)\n" +
                    "• bili: — Bilibili\n" +
                    "• nico: — Niconico\n" +
                    "• yt: — YouTube (ook de standaard zonder voorvoegsel)\n" +
                    "Playlists zoeken met ! werkt alleen op YouTube. Niet elke site levert " +
                    "thumbnails of een duur mee; dan blijft dat veld leeg."),
                ("Mappenstructuur bij zoeken",
                    "Video's die je via zoeken toevoegt worden opgeslagen in:\n" +
                    "Outputmap \\ Zoekterm \\ Playlistnaam \\ video.mp4"),
            ]),
            ("Wachtrij beheren", [
                ("Annuleren",
                    "Klik op de Annuleren knop naast een actieve download om deze te stoppen."),
                ("Opnieuw proberen",
                    "Bij een mislukte download klik je op Opnieuw proberen om de download " +
                    "nogmaals te starten."),
                ("Verwijderen",
                    "Voltooide downloads worden automatisch na 10 seconden uit de lijst verwijderd. " +
                    "Je kunt een item ook handmatig verwijderen via rechtermuisknop → Verwijderen."),
            ]),
            ("Instellingen", [
                ("Outputmap",
                    "De map waar gedownloade bestanden worden opgeslagen. " +
                    "Standaard: Documenten\\Downloads\\YtDlpQueue."),
                ("Max. gelijktijdige downloads",
                    "Hoeveel downloads tegelijk mogen draaien. Een hogere waarde is sneller " +
                    "maar verbruikt meer bandbreedte. Wijziging vereist herstart van de app."),
                ("Cookies",
                    "Voor leeftijdsgevoelige of privé-video's zijn cookies nodig. " +
                    "Gebruik Inloggen bij YouTube om cookies op te slaan."),
                ("Tools updaten",
                    "Via Instellingen kun je controleren op updates voor yt-dlp, ffmpeg en deno. " +
                    "De oude versie wordt hernoemd (niet verwijderd) zodat je eenvoudig kunt terugzetten."),
            ]),
        };

        var content = BuildScrollContent(sections, 580);
        scroll.Controls.Add(content);
        page.Controls.Add(scroll);
        return page;
    }

    private static TabPage BuildProbleemoplossingTab()
    {
        var page = new TabPage("Problemen oplossen");
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };

        var sections = new (string Title, (string Label, string Text)[] Items)[]
        {
            ("Downloaden mislukt", [
                ("YouTube 403 / toegang geweigerd",
                    "YouTube vereist dat je bent ingelogd. Ga naar Instellingen → " +
                    "Inloggen bij YouTube, log in en sla de cookies op. " +
                    "Probeer de download daarna opnieuw."),
                ("Cookies verlopen",
                    "Cookies zijn na verloop van tijd niet meer geldig. " +
                    "Log opnieuw in via Instellingen → Inloggen bij YouTube."),
                ("Cookies-bestand ongeldig",
                    "Als het cookies-bestand corrupt is verschijnt er een melding. " +
                    "Verwijder het bestand en log opnieuw in via Instellingen."),
                ("yt-dlp exitcode fout",
                    "Als yt-dlp een foutcode geeft staat de exacte foutmelding in de " +
                    "statuskolom. Kopieer de fout via rechtermuisknop → Foutmelding kopiëren."),
            ]),
            ("Zoeken werkt niet", [
                ("Geen resultaten gevonden",
                    "Controleer of je internetverbinding werkt. Als je een bedrijfsnetwerk " +
                    "gebruikt kan een proxy of firewall YouTube blokkeren."),
                ("Cookies-bestand fout bij zoeken",
                    "Hetzelfde cookies-bestand wordt gebruikt voor zoeken en downloaden. " +
                    "Vernieuw de cookies via Instellingen → Inloggen bij YouTube."),
            ]),
            ("Afspeelproblemen", [
                ("Audio niet afspeelbaar (Opus)",
                    "Kies bij het toevoegen voor Video (MP4) of Audio (mp3) in plaats van " +
                    "Video (beste kwaliteit). MP4 gebruikt H.264/AAC die altijd afspeelbaar is " +
                    "in Windows Media Player."),
                ("Video opent niet",
                    "Controleer of het bestand volledig gedownload is (status: Voltooid). " +
                    "Installeer VLC Media Player voor de beste compatibiliteit."),
            ]),
            ("Ingebouwde browser (YouTube inloggen)", [
                ("WebView2 ontbreekt",
                    "De app vraagt automatisch om WebView2 te installeren als het ontbreekt. " +
                    "Klik op Ja en volg de installatie-instructies."),
                ("Browser start niet",
                    "Als de browser niet start na WebView2 installatie, herstart dan de app " +
                    "en probeer opnieuw via Instellingen → Inloggen bij YouTube."),
            ]),
            ("Tools", [
                ("yt-dlp, ffmpeg of deno ontbreekt",
                    "De tools worden geïnstalleerd in de tools-map naast de app. " +
                    "Controleer via Instellingen of de tools aanwezig zijn en gebruik " +
                    "de Update knoppen om de nieuwste versie te downloaden."),
                ("Update mislukt",
                    "Als een update mislukt, download dan de nieuwste versie handmatig:\n" +
                    "• yt-dlp: github.com/yt-dlp/yt-dlp/releases\n" +
                    "• ffmpeg: github.com/GyanD/codexffmpeg/releases\n" +
                    "• deno: github.com/denoland/deno/releases\n" +
                    "Plaats het bestand in de tools-map naast de app."),
            ]),
        };

        var content = BuildScrollContent(sections, 580);
        scroll.Controls.Add(content);
        page.Controls.Add(scroll);
        return page;
    }

    private static Control BuildScrollContent(
        (string Title, (string Label, string Text)[] Items)[] sections,
        int width)
    {
        var panel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            Dock = DockStyle.Top,
            Padding = new Padding(12, 8, 12, 8),
            Width = width,
        };

        foreach (var (title, items) in sections)
        {
            // Sectie header
            var header = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                AutoSize = false,
                Width = width - 32,
                Height = 24,
                Margin = new Padding(0, 12, 0, 4),
            };
            panel.Controls.Add(header);

            var rule = new Panel
            {
                Height = 1,
                Width = width - 32,
                BackColor = SystemColors.ControlLight,
                Margin = new Padding(0, 0, 0, 6),
            };
            panel.Controls.Add(rule);

            foreach (var (label, text) in items)
            {
                // Vraag / onderwerp label
                var questionLabel = new Label
                {
                    Text = label,
                    Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                    AutoSize = false,
                    Width = width - 32,
                    Height = 18,
                    Margin = new Padding(0, 4, 0, 2),
                    ForeColor = SystemColors.ControlDarkDark,
                };
                panel.Controls.Add(questionLabel);

                // Antwoord / uitleg tekst
                var answerLabel = new Label
                {
                    Text = text,
                    AutoSize = false,
                    Width = width - 48,
                    Margin = new Padding(8, 0, 0, 4),
                    ForeColor = SystemColors.ControlText,
                };

                // Bereken de juiste hoogte op basis van de tekst
                using var g = answerLabel.CreateGraphics();
                var size = g.MeasureString(text, answerLabel.Font,
                    new SizeF(width - 48, float.MaxValue));
                answerLabel.Height = (int)Math.Ceiling(size.Height) + 4;

                panel.Controls.Add(answerLabel);
            }
        }

        return panel;
    }
}