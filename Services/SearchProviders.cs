using System.Text.RegularExpressions;

namespace YtDlpQueueDesktop.Services;

/// <summary>
/// Een site waarop gezocht kan worden. Er zijn twee soorten:
/// - <see cref="SearchKey"/>: een ingebouwde yt-dlp zoekfunctie, bijv. "scsearch" → "scsearch12:zoekterm".
/// - <see cref="UrlTemplate"/>: een zoekresultatenpagina die yt-dlp als playlist herkent,
///   {0} wordt vervangen door de URL-gecodeerde zoekterm.
/// <see cref="Hidden"/> = werkt wel, maar wordt nergens in de UI of help als optie getoond.
/// </summary>
public sealed record SearchProvider(
    string Prefix,
    string Name,
    string? SearchKey = null,
    string? UrlTemplate = null,
    bool SupportsPlaylists = false,
    bool Hidden = false)
{
    public bool IsYoutube => SearchKey == "ytsearch";

    /// <summary>Het argument dat aan yt-dlp wordt meegegeven.</summary>
    public string BuildTarget(string query, int count) =>
        SearchKey is not null
            ? $"{SearchKey}{count}:{query}"
            : string.Format(UrlTemplate!, Uri.EscapeDataString(query));
}

/// <summary>
/// Bekende zoekvoorvoegsels. Typ in het invoerveld bijv. "sc: lofi beats" om op SoundCloud te
/// zoeken. Zonder (bekend) voorvoegsel wordt op YouTube gezocht.
/// </summary>
public static class SearchProviders
{
    public static readonly SearchProvider YouTube =
        new("yt", "YouTube", SearchKey: "ytsearch", SupportsPlaylists: true);

    public static readonly IReadOnlyList<SearchProvider> All =
    [
        YouTube,
        new("sc", "SoundCloud", SearchKey: "scsearch"),
        // Google Video (gvsearch) bewust weggelaten: leverde in tests geen resultaten op.
        new("bili", "Bilibili", SearchKey: "bilisearch"),
        new("nico", "Niconico", SearchKey: "nicosearch"),
        new("ph", "Pornhub", UrlTemplate: "https://www.pornhub.com/video/search?search={0}", Hidden: true),
    ];

    private static readonly Regex PrefixRegex =
        new(@"^\s*(?<prefix>[a-zA-Z]{2,6}):\s*(?<query>.+)$", RegexOptions.Compiled);

    /// <summary>
    /// Splitst de invoer in een zoeksite en zoekterm. Alleen een bekend voorvoegsel telt; een
    /// zoekterm als "Hoofdstuk 3: de start" blijft dus gewoon een YouTube-zoekopdracht.
    /// </summary>
    public static (SearchProvider Provider, string Query) Parse(string input)
    {
        var trimmed = input.Trim();
        var match = PrefixRegex.Match(trimmed);

        if (match.Success)
        {
            var prefix = match.Groups["prefix"].Value;
            var provider = All.FirstOrDefault(p =>
                string.Equals(p.Prefix, prefix, StringComparison.OrdinalIgnoreCase));

            if (provider is not null)
                return (provider, match.Groups["query"].Value.Trim());
        }

        return (YouTube, trimmed);
    }

    /// <summary>Korte uitleg voor tooltip/help, bijv. "sc: = SoundCloud".</summary>
    public static string Overview =>
        string.Join(Environment.NewLine, All.Where(p => !p.Hidden).Select(p => $"{p.Prefix}:  {p.Name}"));
}
