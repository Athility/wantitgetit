namespace StreamDesk.Core;

/// <summary>A single episode of a show or anime series.</summary>
public sealed class EpisodeInfo
{
    public EpisodeInfo()
    {
    }

    public EpisodeInfo(string id, string providerId, string seriesId, int seasonNumber, int episodeNumber, string title)
    {
        Id = id;
        ProviderId = providerId;
        SeriesId = seriesId;
        SeasonNumber = seasonNumber;
        EpisodeNumber = episodeNumber;
        Title = title;
    }

    public string Id { get; set; } = string.Empty;

    public string ProviderId { get; set; } = string.Empty;

    public string SeriesId { get; set; } = string.Empty;

    public int SeasonNumber { get; set; }

    public int EpisodeNumber { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Duration in seconds, when known.</summary>
    public int? DurationSeconds { get; set; }

    public DateTime? AirDate { get; set; }

    public string? ThumbnailUrl { get; set; }

    /// <summary>Direct local path when the episode comes from the local library.</summary>
    public string? LocalPath { get; set; }
}
