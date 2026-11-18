using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace YtDlpQueueDesktop;

/// <summary>
/// Toont informatie over yt-dlp Queue en biedt de mogelijkheid tot een donatie.
/// </summary>
public class AboutForm : Form
{
    public AboutForm()
    {
        Text = "Over yt-dlp Queue";
        ClientSize = new Size(480, 520);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowIcon = false;
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = SystemColors.Window;

        var layout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(24, 20, 24, 20),
            AutoScroll = true,
        };

        // ── App naam en versie ────────────────────────────────────────────
        var titleLabel = new Label
        {
            Text = "yt-dlp Queue",
            Font = new Font("Segoe UI", 16f, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 4),
        };
        layout.Controls.Add(titleLabel);

        var version = Assembly.GetExecutingAssembly()
            .GetName().Version?.ToString(3) ?? "1.1.0";

        var versionLabel = new Label
        {
            Text = $"Versie {version}",
            ForeColor = SystemColors.GrayText,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 4),
        };
        layout.Controls.Add(versionLabel);

        var authorLabel = new Label
        {
            Text = "© 2026 GS Software — Gijsbert Sonnevelt",
            ForeColor = SystemColors.GrayText,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 16),
        };
        layout.Controls.Add(authorLabel);

        var descLabel = new Label
        {
            Text = "yt-dlp Queue is een gratis downloadmanager voor YouTube, " +
                   "gebouwd op yt-dlp. Als je de app nuttig vindt, overweeg " +
                   "dan een kleine donatie om de ontwikkeling te ondersteunen.",
            AutoSize = false,
            Width = 432,
            Height = 56,
            Margin = new Padding(0, 0, 0, 20),
        };
        layout.Controls.Add(descLabel);

        // ── Scheidingslijn ────────────────────────────────────────────────
        layout.Controls.Add(new Panel
        {
            Height = 1,
            Width = 432,
            BackColor = SystemColors.ControlLight,
            Margin = new Padding(0, 0, 0, 16),
        });

        // ── Donatie sectie ────────────────────────────────────────────────
        var donateHeader = new Label
        {
            Text = "Doneren",
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 8),
        };
        layout.Controls.Add(donateHeader);

        // QR codes naast elkaar
        var qrPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 12),
        };

        // PayPal QR
        var paypalPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            AutoSize = true,
            Margin = new Padding(0, 0, 24, 0),
        };

        var paypalQr = LoadQrImage("donate-paypal.png", 180);
        if (paypalQr is not null)
            paypalPanel.Controls.Add(paypalQr);

        var paypalLabel = new Label
        {
            Text = "PayPal",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(0, 4, 0, 0),
        };
        paypalPanel.Controls.Add(paypalLabel);

        var paypalSubLabel = new Label
        {
            Text = "Scan met PayPal-app",
            ForeColor = SystemColors.GrayText,
            AutoSize = true,
        };
        paypalPanel.Controls.Add(paypalSubLabel);
        qrPanel.Controls.Add(paypalPanel);

        // Bank QR (EPC/SEPA - werkt met vrijwel elke bank-app)
        var bankPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            AutoSize = true,
        };

        var bankQr = LoadQrImage("donate-bank-epc.png", 180);
        if (bankQr is not null)
            bankPanel.Controls.Add(bankQr);

        var bankLabel = new Label
        {
            Text = "Bankoverschrijving",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(0, 4, 0, 0),
        };
        bankPanel.Controls.Add(bankLabel);

        var bankSubLabel = new Label
        {
            Text = "Scan met je eigen bank-app",
            ForeColor = SystemColors.GrayText,
            AutoSize = true,
        };
        bankPanel.Controls.Add(bankSubLabel);
        qrPanel.Controls.Add(bankPanel);

        layout.Controls.Add(qrPanel);

        // ── Sluiten knop ──────────────────────────────────────────────────
        layout.Controls.Add(new Panel
        {
            Height = 1,
            Width = 432,
            BackColor = SystemColors.ControlLight,
            Margin = new Padding(0, 8, 0, 12),
        });

        var closeButton = new Button
        {
            Text = "Sluiten",
            Width = 100,
            Height = 32,
            DialogResult = DialogResult.OK,
        };
        layout.Controls.Add(closeButton);

        Controls.Add(layout);
        AcceptButton = closeButton;
    }

    private static PictureBox? LoadQrImage(string fileName, int size)
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));

            if (resourceName is null) return null;

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream is null) return null;

            var image = Image.FromStream(stream);
            return new PictureBox
            {
                Image = image,
                Size = new Size(size, size),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.White,
            };
        }
        catch
        {
            return null;
        }
    }
}
