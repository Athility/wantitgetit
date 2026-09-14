namespace StreamDesk.Core;

/// <summary>
/// Lightweight catalog item used by grids, search results and rows.
/// <see cref="Id"/> is provider-qualified, e.g. <c>tmdb:movie:603</c>.
/// </summary>
public sealed class MediaSummary
{
    public MediaSummary()
    {
    }

    public MediaSummary(string id, string providerId, MediaType mediaType, string title)
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

    public int? Year { get; set; }

    /// <summary>Poster image URL or local path.</summary>
    public string? PosterUrl { get; set; }

    /// <summary>Average rating (0-10 scale where available).</summary>
    public double? Rating { get; set; }

    /// <summary>Free-form provider tag, e.g. "Anime" or "Local".</summary>
    public string? Category { get; set; }
}
