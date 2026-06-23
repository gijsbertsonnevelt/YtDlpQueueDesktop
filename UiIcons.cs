using System.Reflection;
using System.Drawing;

namespace YtDlpQueueDesktop;

public static class UiIcons
{
    private static readonly Assembly Assembly = Assembly.GetExecutingAssembly();

    public static readonly Icon Main = Load("YtDlpQueue.ico");
    public static readonly Icon Settings = Load("global-settings.ico");
    public static readonly Icon Youtube = Load("logon.ico");

    private static Icon Load(string fileName)
    {
        var names = Assembly.GetManifestResourceNames();

        var resourceName = names
            .FirstOrDefault(n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));

        if (resourceName == null)
        {
            var all = string.Join("\n", names);
            throw new Exception($"Icon niet gevonden: {fileName}\nBeschikbaar:\n{all}");
        }

        using var stream = Assembly.GetManifestResourceStream(resourceName);
        return new Icon(stream!);
    }
}