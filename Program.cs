using System.Windows.Forms;

namespace YtDlpQueueDesktop;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // ✅ Zorg dat tools-map bestaat
        ToolPaths.EnsureToolsDir();

        // ✅ Verplaats oude cookies indien nodig
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm());
    }
}