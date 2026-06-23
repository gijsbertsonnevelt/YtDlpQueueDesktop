using System.Globalization;

namespace YtDlpQueueDesktop.Services;

public enum CookieStatus
{
    Missing,
    Empty,
    Invalid,
    SessionOnly,
    Expired,
    ExpiringSoon,
    Valid
}

public sealed class CookieInspectionResult
{
    public CookieStatus Status { get; init; }
    public string Message { get; init; } = string.Empty;
    public DateTimeOffset? LatestExpiryUtc { get; init; }

    public bool IsUsable =>
        Status == CookieStatus.Valid ||
        Status == CookieStatus.ExpiringSoon;
}

public static class CookieFileInspector
{
    /// <summary>
    /// Inspecteert een Netscape cookies.txt bestand en bepaalt of het bruikbaar is.
    /// Let op: dit is een best-effort lokale controle op basis van expiry timestamps.
    /// Server-side invalidatie door Google/YouTube kan hiermee niet gegarandeerd gedetecteerd worden.
    /// </summary>
    public static CookieInspectionResult Inspect(string path, TimeSpan? expiringSoonWindow = null)
    {
        expiringSoonWindow ??= TimeSpan.FromDays(7);

        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return new CookieInspectionResult
            {
                Status = CookieStatus.Missing,
                Message = "Cookies ontbreken"
            };
        }

        string[] lines;
        try
        {
            lines = File.ReadAllLines(path);
        }
        catch
        {
            return new CookieInspectionResult
            {
                Status = CookieStatus.Invalid,
                Message = "Cookiesbestand is niet leesbaar"
            };
        }

        if (lines.Length == 0)
        {
            return new CookieInspectionResult
            {
                Status = CookieStatus.Empty,
                Message = "Cookiesbestand is leeg"
            };
        }

        var expiryDates = new List<DateTimeOffset>();
        var sawCookieLine = false;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();

            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (line.StartsWith("#"))
                continue;

            // Netscape cookie file heeft normaal 7 tab-gescheiden kolommen:
            // domain, include_subdomains, path, secure, expiry, name, value
            var parts = line.Split('\t');

            // Sommige exports gebruiken spaties i.p.v. tabs; probeer daar voorzichtig mee om te gaan
            if (parts.Length < 7)
            {
                parts = line
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries);
            }

            if (parts.Length < 7)
                continue;

            sawCookieLine = true;

            var expiryRaw = parts[4].Trim();

            if (!long.TryParse(expiryRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unix))
                continue;

            // 0 = sessiecookie, die negeren we voor "langst geldige expiry"
            if (unix <= 0)
                continue;

            try
            {
                expiryDates.Add(DateTimeOffset.FromUnixTimeSeconds(unix));
            }
            catch
            {
                // Ongeldige timestamp: negeren
            }
        }

        if (!sawCookieLine)
        {
            return new CookieInspectionResult
            {
                Status = CookieStatus.Invalid,
                Message = "Geen geldige cookies gevonden"
            };
        }

        if (expiryDates.Count == 0)
        {
            return new CookieInspectionResult
            {
                Status = CookieStatus.SessionOnly,
                Message = "Alleen sessiecookies aanwezig"
            };
        }

        var now = DateTimeOffset.UtcNow;
        var latestExpiry = expiryDates.Max();

        if (latestExpiry <= now)
        {
            return new CookieInspectionResult
            {
                Status = CookieStatus.Expired,
                Message = $"Cookies verlopen op {latestExpiry.LocalDateTime:dd-MM-yyyy HH:mm}",
                LatestExpiryUtc = latestExpiry
            };
        }

        if (latestExpiry <= now.Add(expiringSoonWindow.Value))
        {
            return new CookieInspectionResult
            {
                Status = CookieStatus.ExpiringSoon,
                Message = $"Cookies verlopen bijna ({latestExpiry.LocalDateTime:dd-MM-yyyy HH:mm})",
                LatestExpiryUtc = latestExpiry
            };
        }

        return new CookieInspectionResult
        {
            Status = CookieStatus.Valid,
            Message = $"Cookies geldig tot {latestExpiry.LocalDateTime:dd-MM-yyyy HH:mm}",
            LatestExpiryUtc = latestExpiry
        };
    }
}
