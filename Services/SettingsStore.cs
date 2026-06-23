using System.Text.Json;
using YtDlpQueueDesktop.Models;

namespace YtDlpQueueDesktop.Services;

public static class SettingsStore
{
    private static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GS Software", "yt-dlp Queue");

    private static readonly string ConfigPath = Path.Combine(ConfigDir, "config.json");

    // Oud pad van vóór de hernoeming naar "yt-dlp Queue"
    private static readonly string LegacyConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GS Software", "YtDlpQueue");

    private static readonly string LegacyConfigPath = Path.Combine(LegacyConfigDir, "config.json");

    public static AppSettings Load()
    {
        // Migreer instellingen van de oude map als die nog bestaan
        MigrateLegacyConfigIfNeeded();

        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                if (settings is not null)
                    return settings;
            }
        }
        catch
        {
            // Corrupt of onleesbaar config-bestand: terugvallen op standaardwaarden.
        }

        return new AppSettings();
    }

    public static void Save(AppSettings settings)
    {
        Directory.CreateDirectory(ConfigDir);
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(ConfigPath, json);
    }

    /// <summary>
    /// Kopieert config.json van de oude YtDlpQueue map naar de nieuwe yt-dlp Queue map
    /// als de nieuwe map nog geen config heeft. Wordt eenmalig uitgevoerd bij de eerste
    /// start na de update.
    /// </summary>
    private static void MigrateLegacyConfigIfNeeded()
    {
        try
        {
            if (File.Exists(ConfigPath)) return;           // nieuwe config bestaat al
            if (!File.Exists(LegacyConfigPath)) return;   // geen oude config om te migreren

            Directory.CreateDirectory(ConfigDir);
            File.Copy(LegacyConfigPath, ConfigPath);

            // Corrigeer het cookies-pad in de gemigreerde config als het nog naar de oude
            // map wijst — het nieuwe pad is altijd de tools-map naast de exe.
            FixCookiesPathAfterMigration();
        }
        catch
        {
            // Migratie mislukt; geen probleem, app start met standaardwaarden.
        }
    }

    private static void FixCookiesPathAfterMigration()
    {
        try
        {
            if (!File.Exists(ConfigPath)) return;

            var json = File.ReadAllText(ConfigPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json);
            if (settings is null) return;

            // Als het cookies-pad nog naar de oude map wijst of niet bestaat,
            // resetten zodat de gebruiker opnieuw kan instellen via de app.
            if (!string.IsNullOrWhiteSpace(settings.CookiesFilePath) &&
                !File.Exists(settings.CookiesFilePath))
            {
                settings.CookiesFilePath = string.Empty;
                var newJson = JsonSerializer.Serialize(settings,
                    new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ConfigPath, newJson);
            }
        }
        catch
        {
            // Stil falen; gebruiker kan cookies opnieuw instellen.
        }
    }
}