using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using StreamDesk.Application.Providers;
using StreamDesk.Core;
using StreamDesk.Infrastructure.Database;

namespace StreamDesk.Providers.M3U;

/// <summary>
/// Live TV provider over user-imported M3U/M3U8 playlists. Only streams the
/// user configured themselves are ever played; no playlists ship with the app.
/// </summary>
public sealed class M3UProvider : IMediaProvider, IPlaylistProvider, ISearchProvider, ISelfTestProvider
{
    private static readonly ProviderDescriptor BaseDescriptor = new(
        ProviderIds.M3U, "M3U Playlists", "Live TV channels from your own M3U/M3U8 playlists.")
    {
        Capabilities = ProviderCapabilities.LiveTV | ProviderCapabilities.Search | ProviderCapabilities.Playback,
        IsEnabled = true
    };

    private readonly PlaylistsRepository _playlists;
    private readonly ChannelsRepository _channels;
    private readonly Func<HttpClient> _httpClientFactory;

    public M3UProvider(PlaylistsRepository playlists, ChannelsRepository channels, Func<HttpClient>? httpClientFactory = null)
    {
        _playlists = playlists;
        _channels = channels;
        _httpClientFactory = httpClientFactory ?? HttpClientHolder.Create;
        Descriptor = BaseDescriptor;
    }

    private static class HttpClientHolder
    {
        internal static readonly Func<HttpClient> Create = () => StreamDesk.Infrastructure.Networking.HttpClientFactory.Create("StreamDesk-M3U/1.0");
    }

    public ProviderDescriptor Descriptor { get; private set; }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var hasPlaylists = _playlists.GetAll().Any(p => p.Enabled);
        Descriptor = new ProviderDescriptor(ProviderIds.M3U, BaseDescriptor.DisplayName, BaseDescriptor.Description)
        {
            Capabilities = BaseDescriptor.Capabilities,
            IsConfigured = hasPlaylists,
            IsEnabled = true,
            HomeSections = Array.Empty<string>()
        };
        return Task.CompletedTask;
    }

    public Task<MediaPage> GetSectionAsync(string sectionName, int page, CancellationToken cancellationToken = default)
    {
        // Live TV is browsed on its own page, not through catalog sections.
        return Task.FromResult(new MediaPage(Array.Empty<MediaSummary>(), page, hasMore: false));
    }

    public Task<MediaDetails?> GetDetailsAsync(string itemId, CancellationToken cancellationToken = default)
    {
        // Channels play directly; no details page is needed.
        return Task.FromResult<MediaDetails?>(null);
    }

    public Task<IReadOnlyList<SeasonInfo>?> GetSeasonsAsync(string itemId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<SeasonInfo>?>(null);
    }

    public async Task<IReadOnlyList<ChannelInfo>> GetChannelsAsync(CancellationToken cancellationToken = default)
    {
        var channels = new List<ChannelInfo>();
        foreach (var playlist in _playlists.GetAll().Where(p => p.Enabled))
        {
            channels.AddRange(await LoadPlaylistChannelsAsync(playlist, forceRefresh: false, cancellationToken).ConfigureAwait(false));
        }

        return channels;
    }

    public async Task<IReadOnlyList<string>> GetGroupsAsync(CancellationToken cancellationToken = default)
    {
        return (await GetChannelsAsync(cancellationToken).ConfigureAwait(false))
            .Select(c => c.Group)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task RefreshAsync(string? playlistId = null, CancellationToken cancellationToken = default)
    {
        var playlists = _playlists.GetAll()
            .Where(p => p.Enabled && (playlistId is null || p.Id == playlistId));
        foreach (var playlist in playlists)
        {
            var channels = await LoadPlaylistChannelsAsync(playlist, forceRefresh: true, cancellationToken).ConfigureAwait(false);
            _channels.ReplaceForPlaylist(playlist.Id, channels);
        }
    }

    public Task<IReadOnlyList<MediaSummary>> SearchAsync(SearchRequest request, CancellationToken cancellationToken = default)
    {
        var term = request.Query.Trim();
        var matches = GetChannelsAsync(cancellationToken).Result
            .Where(c => c.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
                        || c.Group.Contains(term, StringComparison.OrdinalIgnoreCase))
            .Take(50)
            .Select(c => new MediaSummary($"m3u:channel:{c.Id}", ProviderIds.M3U, MediaType.Channel, c.Name)
            {
                PosterUrl = c.LogoUrl,
                Category = c.Group
            })
            .ToList();
        return Task.FromResult<IReadOnlyList<MediaSummary>>(matches);
    }

    public async Task<ProviderTestResult> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        var playlists = _playlists.GetAll().Where(p => p.Enabled).ToList();
        if (playlists.Count == 0)
        {
            return new ProviderTestResult(false, "No playlists configured. Import one in Settings \u2192 Live TV.");
        }

        var failures = 0;
        foreach (var playlist in playlists)
        {
            try
            {
                var channels = await LoadPlaylistChannelsAsync(playlist, forceRefresh: true, cancellationToken).ConfigureAwait(false);
                if (channels.Count == 0)
                {
                    failures++;
                }
            }
            catch (Exception)
            {
                failures++;
            }
        }

        return failures == 0
            ? new ProviderTestResult(true, $"All {playlists.Count} playlist(s) loaded.")
            : new ProviderTestResult(false, $"{failures} of {playlists.Count} playlist(s) failed to load.");
    }

    private async Task<IReadOnlyList<ChannelInfo>> LoadPlaylistChannelsAsync(PlaylistSource playlist, bool forceRefresh, CancellationToken cancellationToken)
    {
        if (!forceRefresh && !playlist.IsRemote)
        {
            var cached = _channels.GetByPlaylist(playlist.Id);
            if (cached.Count > 0)
            {
                return cached;
            }
        }

        var parseResult = await FetchAndParseAsync(playlist, cancellationToken).ConfigureAwait(false);
        var channels = parseResult.Channels
            .Where(c => !string.IsNullOrWhiteSpace(c.Url))
            .Select((c, i) => new ChannelInfo(
                $"{playlist.Id}:{i}",
                c.Name,
                c.Url)
            {
                Group = c.Group,
                LogoUrl = c.LogoUrl
            })
            .ToList();

        playlist.LastRefreshedUtc = DateTime.UtcNow;
        _playlists.Upsert(playlist);
        _channels.ReplaceForPlaylist(playlist.Id, channels);
        return channels;
    }

    private async Task<M3UParseResult> FetchAndParseAsync(PlaylistSource playlist, CancellationToken cancellationToken)
    {
        if (playlist.IsRemote)
        {
            using var client = _httpClientFactory();
            var content = await client.GetStringAsync(playlist.Url, cancellationToken).ConfigureAwait(false);
            return M3UParser.ParseText(content);
        }

        return M3UParser.ParseFile(playlist.FilePath);
    }
}
