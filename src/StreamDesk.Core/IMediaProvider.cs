using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace StreamDesk.Core;

/// <summary>Page of results returned by paged provider operations.</summary>
public sealed class MediaPage
{
    public MediaPage()
    {
    }

    public MediaPage(IReadOnlyList<MediaSummary> items, int page, bool hasMore)
    {
        Items = items;
        Page = page;
        HasMore = hasMore;
    }

    public IReadOnlyList<MediaSummary> Items { get; set; } = Array.Empty<MediaSummary>();

    public int Page { get; set; } = 1;

    public bool HasMore { get; set; }
}

/// <summary>Search request passed to providers.</summary>
public sealed class SearchRequest
{
    public SearchRequest(string query, int page = 1)
    {
        Query = query;
        Page = page;
    }

    public string Query { get; }

    public int Page { get; }

    /// <summary>Restrict results to these media types; empty means all.</summary>
    public IReadOnlyList<MediaType>? Types { get; init; }
}

/// <summary>
/// Contract for a media source. Implementations must be safe to call when
/// unconfigured - throw <see cref="ProviderNotConfiguredException"/> instead of
/// returning fake data. UI code never talks to a concrete provider, only to this.
/// </summary>
public interface IMediaProvider
{
    /// <summary>Provider identity and declared capabilities.</summary>
    ProviderDescriptor Descriptor { get; }

    /// <summary>
    /// Load/refresh configuration and compute <see cref="ProviderDescriptor.IsConfigured"/>.
    /// Called by the <c>ProviderManager</c> at startup and after settings change.
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>Items for a named Home section (see <see cref="ProviderSectionNames"/>).</summary>
    Task<MediaPage> GetSectionAsync(string sectionName, int page, CancellationToken cancellationToken = default);

    /// <summary>Full metadata for one item.</summary>
    Task<MediaDetails?> GetDetailsAsync(string itemId, CancellationToken cancellationToken = default);

    /// <summary>Season/episode listing for a show. Returns null for movies.</summary>
    Task<IReadOnlyList<SeasonInfo>?> GetSeasonsAsync(string itemId, CancellationToken cancellationToken = default);
}

/// <summary>Search capability; implemented by providers supporting <c>ProviderCapabilities.Search</c>.</summary>
public interface ISearchProvider
{
    /// <summary>Search across the provider's catalog.</summary>
    Task<IReadOnlyList<MediaSummary>> SearchAsync(SearchRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Playback capability: resolve a legitimate source for an item.</summary>
public interface IPlaybackProvider
{
    /// <summary>
    /// Resolve the playable source for an item id (movie or episode).
    /// Returns a local file path or a URL from an authorized source. Never bypasses DRM.
    /// </summary>
    Task<PlaybackSource?> ResolvePlaybackAsync(string itemId, CancellationToken cancellationToken = default);
}

/// <summary>Subtitle capability: enumerate tracks for an item.</summary>
public interface ISubtitleProvider
{
    /// <summary>Available subtitle tracks for the given item, ordered by preference.</summary>
    Task<IReadOnlyList<SubtitleTrack>> GetSubtitleTracksAsync(string itemId, CancellationToken cancellationToken = default);
}

/// <summary>Download capability for providers where downloading is authorized.</summary>
public interface IDownloadProvider
{
    /// <summary>
    /// Resolve a downloadable HTTP(S) source for an item, plus target file name.
    /// Returns null when the item cannot be downloaded legally/permitted.
    /// </summary>
    Task<(PlaybackSource Source, string SuggestedFileName)?> ResolveDownloadAsync(string itemId, CancellationToken cancellationToken = default);
}

/// <summary>Live TV capability backed by user-provided playlists.</summary>
public interface IPlaylistProvider
{
    /// <summary>All channels across configured playlists, grouped by <see cref="ChannelInfo.Group"/>.</summary>
    Task<IReadOnlyList<ChannelInfo>> GetChannelsAsync(CancellationToken cancellationToken = default);

    /// <summary>Distinct group names across configured playlists.</summary>
    Task<IReadOnlyList<string>> GetGroupsAsync(CancellationToken cancellationToken = default);

    /// <summary>Force re-download/re-parse of the given playlist; all when null.</summary>
    Task RefreshAsync(string? playlistId = null, CancellationToken cancellationToken = default);
}

/// <summary>Result of a provider self-test (Settings → Test Connection).</summary>
public sealed class ProviderTestResult
{
    public ProviderTestResult()
    {
    }

    public ProviderTestResult(bool success, string? message = null)
    {
        Success = success;
        Message = message;
    }

    public bool Success { get; set; }

    public string? Message { get; set; }
}

/// <summary>Optional capability: providers that can verify their configuration.</summary>
public interface ISelfTestProvider
{
    /// <summary>Verify the provider can operate; returns an honest result.</summary>
    Task<ProviderTestResult> TestConnectionAsync(CancellationToken cancellationToken = default);
}
