namespace StreamDesk.Core;

/// <summary>Full metadata for a movie, show or anime series.</summary>
public sealed class MediaDetails
{
    public MediaDetails()
    {
    }

    public MediaDetails(string id, string providerId, MediaType mediaType, string title)
    {
        Id = id;
        ProviderId = providerId;
        MediaType = mediaType;
        Title = title;
    }

    public string Id { get; set; } = string.Empty;

    public string ProviderId { get; set; } = string.Empty;

    public MediaType MediaType { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? OriginalTitle { get; set; }

    public string? Description { get; set; }

    public string? Tagline { get; set; }

    public int? Year { get; set; }

    public DateTime? ReleaseDate { get; set; }

    public double? Rating { get; set; }

    /// <summary>Runtime in minutes (movies), or average episode runtime (shows).</summary>
    public int? RuntimeMinutes { get; set; }

    public IReadOnlyList<string> Genres { get; set; } = Array.Empty<string>();

    public IReadOnlyList<CastMember> Cast { get; set; } = Array.Empty<CastMember>();

    public string? PosterUrl { get; set; }

    public string? BackdropUrl { get; set; }

    /// <summary>Seasons for shows/anime; empty for movies.</summary>
    public IReadOnlyList<SeasonInfo> Seasons { get; set; } = Array.Empty<SeasonInfo>();

    /// <summary>Direct local path when the item lives in the local library.</summary>
    public string? LocalPath { get; set; }
}
