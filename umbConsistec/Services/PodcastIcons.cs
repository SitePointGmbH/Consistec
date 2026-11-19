namespace umbConsistec.Services;

/// <summary>
/// Podcast platform icons served from /assets/img/podcast-icons.
/// Keep in sync with App_Plugins/PodcastIconPicker/podcast-icon-picker.element.js.
/// </summary>
public static class PodcastIcons
{
    private static readonly Dictionary<string, string> Labels = new(StringComparer.Ordinal)
    {
        ["apple-podcasts"] = "Apple Podcasts",
        ["spotify"] = "Spotify",
        ["youtube"] = "YouTube",
        ["youtube-music"] = "YouTube Music",
        ["amazon-music"] = "Amazon Music",
        ["deezer"] = "Deezer",
        ["audible"] = "Audible",
        ["pocket-casts"] = "Pocket Casts",
        ["rss"] = "RSS-Feed",
        ["podcast"] = "Podcast"
    };

    public static bool TryGet(string? key, out string url, out string label)
    {
        if (key is not null && Labels.TryGetValue(key, out label!))
        {
            url = $"/assets/img/podcast-icons/{key}.svg";
            return true;
        }

        url = label = "";
        return false;
    }
}
