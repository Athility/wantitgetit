namespace StreamDesk.Core;

/// <summary>A season of a show or anime series.</summary>
public sealed class SeasonInfo
{
    public SeasonInfo()
    {
    }

    public SeasonInfo(int seasonNumber, string name, IReadOnlyList<EpisodeInfo> episodes)
    {
        SeasonNumber = seasonNumber;
        Name = name;
        Episodes = episodes;
    }

    public int SeasonNumber { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Episodes are ordered by episode number.</summary>
    public IReadOnlyList<EpisodeInfo> Episodes { get; set; } = Array.Empty<EpisodeInfo>();
}
