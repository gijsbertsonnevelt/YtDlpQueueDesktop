using System.IO;
using System.Security.Cryptography.X509Certificates;

namespace YtDlpQueueDesktop.Services;

/// <summary>
/// Zorgt dat het Zscaler root certificaat (indien meegeleverd in de tools-map)
/// geïnstalleerd is in het CurrentUser Root certificaatarchief én als PEM-bestand
/// beschikbaar is voor yt-dlp. Wordt aangeroepen bij het opstarten van de app.
/// </summary>
public static class ZscalerCertInstaller
{
    /// <summary>Pad naar het meegeleverde .cer bestand in de tools-map naast de exe.</summary>
    private static string CerPath =>
        Path.Combine(ToolPaths.ToolsDir, "zscaler-root.cer");

    /// <summary>
    /// Installeert het Zscaler certificaat als dat nodig is. Veilig om bij elke start
    /// aan te roepen: doet niets als het .cer bestand ontbreekt of alles al geregeld is.
    /// Retourneert true als er iets gewijzigd is (cert geïnstalleerd of PEM geschreven).
    /// </summary>
    public static bool EnsureInstalled()
    {
        var changed = false;

        try
        {
            if (!File.Exists(CerPath))
                return false;

            var cert = new X509Certificate2(CerPath);

            // 1. Installeer in CurrentUser\Root als het er nog niet in zit
            //    (Windows toont hierbij eenmalig een bevestigingsdialoog)
            if (!IsCertInstalled(cert.Thumbprint))
            {
                using var store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
                store.Open(OpenFlags.ReadWrite);
                store.Add(cert);
                store.Close();
                changed = true;
            }

            // 2. Schrijf het PEM bestand voor yt-dlp als dat er nog niet is
            if (!File.Exists(ToolPaths.ZscalerCertPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ToolPaths.ZscalerCertPath)!);
                var b64 = Convert.ToBase64String(cert.Export(X509ContentType.Cert));
                var lines = new List<string>
                {
                    "-----BEGIN CERTIFICATE-----",
                    b64,
                    "-----END CERTIFICATE-----"
                };
                File.WriteAllLines(ToolPaths.ZscalerCertPath, lines);
                changed = true;
            }
        }
        catch
        {
            // Certificaat installatie is best-effort; app moet altijd kunnen starten.
            // Gebruiker kan het handmatig doen via Instellingen → Cert vernieuwen.
        }

        return changed;
    }

    /// <summary>Controleert of een certificaat met de gegeven thumbprint al in
    /// CurrentUser\Root staat.</summary>
    private static bool IsCertInstalled(string thumbprint)
    {
        try
        {
            using var store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadOnly);
            var found = store.Certificates
                .Find(X509FindType.FindByThumbprint, thumbprint, validOnly: false)
                .Count > 0;
            store.Close();
            return found;
        }
        catch
        {
            return false;
        }
    }
}
