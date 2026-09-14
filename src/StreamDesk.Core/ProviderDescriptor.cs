using System;
using System.Collections.Generic;

namespace StreamDesk.Core;

/// <summary>Static capability flags a provider can declare.</summary>
[Flags]
public enum ProviderCapabilities
{
    None = 0,

    /// <summary>Browsable/paged movie catalog.</summary>
    Movies = 1 << 0,

    /// <summary>Browsable TV shows.</summary>
    Shows = 1 << 1,

    /// <summary>Browsable anime (may overlap with shows).</summary>
    Anime = 1 << 2,

    /// <summary>Episode-level information for shows.</summary>
    Episodes = 1 << 3,

    /// <summary>Full-text search.</summary>
    Search = 1 << 4,

    /// <summary>Rich metadata (descriptions, cast, ratings).</summary>
    Metadata = 1 << 5,

    /// <summary>Can hand the player a legitimate source for items.</summary>
    Playback = 1 << 6,

    /// <summary>Can supply subtitle tracks.</summary>
    Subtitles = 1 << 7,

    /// <summary>Downloadable items via <see cref="IDownloadProvider"/> where authorized.</summary>
    Downloads = 1 << 8,

    /// <summary>Live TV channels from user-configured playlists.</summary>
    LiveTV = 1 << 9,

    /// <summary>Browsable home sections (trending, popular, recently added...).</summary>
    HomeSections = 1 << 10,

    /// <summary>Provider can resolve favorites/watch-history enrichments.</summary>
    ContinueWatching = 1 << 11
}

/// <summary>Named sections a <see cref="IMediaProvider"/> exposes for the Home page.</summary>
public static class ProviderSectionNames
{
    public const string Trending = "Trending";
    public const string PopularMovies = "Popular Movies";
    public const string PopularShows = "Popular Shows";
    public const string PopularAnime = "Popular Anime";
    public const string RecentlyAdded = "Recently Added";

    /// <summary>All standard catalog section names.</summary>
    public static readonly IReadOnlyList<string> All = new[]
    {
        Trending, PopularMovies, PopularShows, PopularAnime, RecentlyAdded
    };
}

/// <summary>
/// A provider's declared capabilities. <see cref="IsConfigured"/> gates all access:
/// an unconfigured provider must never pretend to work.
/// </summary>
public sealed class ProviderDescriptor
{
    public ProviderDescriptor()
    {
    }

    public ProviderDescriptor(string id, string displayName, string description)
    {
        Id = id;
        DisplayName = displayName;
        Description = description;
    }

    /// <summary>Stable provider identifier, e.g. "tmdb".</summary>
    public string Id { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>Capability flags declared by this provider.</summary>
    public ProviderCapabilities Capabilities { get; set; } = ProviderCapabilities.None;

    /// <summary>
    /// True when the provider has everything it needs to operate (e.g. an API key,
    /// a library folder, a playlist). Unconfigured providers fail gracefully.
    /// </summary>
    public bool IsConfigured { get; set; }

    /// <summary>True when the user enabled the provider.</summary>
    public bool IsEnabled { get; set; }

    /// <summary>Section names this provider can serve, subset of <see cref="ProviderSectionNames.All"/>.</summary>
    public IReadOnlyList<string> HomeSections { get; set; } = Array.Empty<string>();
}
