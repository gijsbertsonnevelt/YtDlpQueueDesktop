using System.Drawing;
using System.Windows.Forms;

namespace YtDlpQueueDesktop;

/// <summary>
/// Toont de versiegeschiedenis van yt-dlp Queue.
/// </summary>
public class ChangelogForm : Form
{
    public ChangelogForm()
    {
        Text = "Changelog — yt-dlp Queue";
        ClientSize = new Size(560, 520);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowIcon = false;
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = SystemColors.Window;

        var scrollHost = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(16, 12, 16, 12),
            BackColor = SystemColors.Window,
        };

        var contentPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            Dock = DockStyle.Top,
        };

        foreach (var entry in Changelog.Entries)
            contentPanel.Controls.Add(BuildEntryPanel(entry, contentPanel));

        scrollHost.Controls.Add(contentPanel);

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

        Controls.Add(scrollHost);
        Controls.Add(separator);
        Controls.Add(closeButton);
    }

    private static Panel BuildEntryPanel(ChangelogEntry entry, FlowLayoutPanel parent)
    {
        var panel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 16),
        };

        // ── Versie + datum header ─────────────────────────────────────────
        var headerPanel = new Panel
        {
            Height = 28,
            Width = 520,
            Margin = new Padding(0, 0, 0, 6),
        };

        var versionLabel = new Label
        {
            Text = $"Versie {entry.Version}",
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(0, 4),
        };

        var dateLabel = new Label
        {
            Text = entry.Date.ToString("dd-MM-yyyy"),
            ForeColor = SystemColors.GrayText,
            AutoSize = true,
            Location = new Point(0, 0),
        };
        dateLabel.Location = new Point(versionLabel.PreferredWidth + 12, 6);

        headerPanel.Controls.Add(versionLabel);
        headerPanel.Controls.Add(dateLabel);
        panel.Controls.Add(headerPanel);

        // ── Scheidingslijn ────────────────────────────────────────────────
        var rule = new Panel
        {
            Height = 1,
            Width = 520,
            BackColor = SystemColors.ControlLight,
            Margin = new Padding(0, 0, 0, 8),
        };
        panel.Controls.Add(rule);

        // ── Secties ───────────────────────────────────────────────────────
        foreach (var section in entry.Sections)
        {
            var sectionLabel = new Label
            {
                Text = section.Title,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = SystemColors.ControlDarkDark,
                AutoSize = true,
                Margin = new Padding(0, 4, 0, 2),
            };
            panel.Controls.Add(sectionLabel);

            foreach (var item in section.Items)
            {
                var itemLabel = new Label
                {
                    Text = $"• {item}",
                    AutoSize = false,
                    Width = 510,
                    Height = 20,
                    Margin = new Padding(8, 0, 0, 0),
                    AutoEllipsis = true,
                };
                panel.Controls.Add(itemLabel);
            }
        }

        return panel;
    }
}