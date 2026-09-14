using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StreamDesk.Core;

namespace StreamDesk.Application.Managers;

/// <summary>Single enabled catalog item for merged views.</summary>
public sealed class MergedItem
{
    public MediaSummary Summary { get; init; } = new();

    public string ProviderId { get; init; } = string.Empty;

    public string ProviderDisplayName { get; init; } = string.Empty;
}

/// <summary>
/// Central registry and router for all providers. The UI only ever talks to
/// this class, never to a concrete provider, so new adapters require no UI
/// changes. Registration order is stable; merge order preserves it.
/// </summary>
public sealed class ProviderManager
{
    private readonly List<ProviderEntry> _entries = new();
    private readonly Func<string, bool> _isEnabled;

    public ProviderManager(Func<string, bool>? isEnabled = null)
    {
        _isEnabled = isEnabled ?? (_ => true);
    }

    private sealed class ProviderEntry
    {
        public required IMediaProvider Provider { get; init; }

        public ProviderDescriptor Descriptor => Provider.Descriptor;

        public ISearchProvider? Search => Provider as ISearchProvider;

        public IPlaybackProvider? Playback => Provider as IPlaybackProvider;

        public ISubtitleProvider? Subtitles => Provider as ISubtitleProvider;

        public IDownloadProvider? Downloads => Provider as IDownloadProvider;

        public IPlaylistProvider? Playlists => Provider as IPlaylistProvider;

        public ISelfTestProvider? SelfTest => Provider as ISelfTestProvider;
    }

    public event Action? ProvidersChanged;

    /// <summary>Register a provider; call InitializeAsync afterwards (or via InitializeAllAsync).</summary>
    public void Register(IMediaProvider provider)
    {
        if (_entries.Any(e => e.Descriptor.Id == provider.Descriptor.Id))
        {
            return;
        }

        _entries.Add(new ProviderEntry { Provider = provider });
    }

    /// <summary>Initialize every provider (loads config, computes IsConfigured).</summary>
    public async Task InitializeAllAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in _entries)
        {
            try
            {
                await entry.Provider.InitializeAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                // A failing provider must never prevent startup.
            }
        }

        ProvidersChanged?.Invoke();
    }

    /// <summary>Re-run initialization after settings change, then notify listeners.</summary>
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        await InitializeAllAsync(cancellationToken).ConfigureAwait(false);
    }

    public IReadOnlyList<ProviderDescriptor> GetDescriptors() =>
        _entries.Select(e => e.Descriptor).ToList();

    public ProviderDescriptor? GetDescriptor(string providerId) =>
        _entries.Select(e => e.Descriptor).FirstOrDefault(d => d.Id == providerId);

    public IReadOnlyList<IMediaProvider> GetEnabledProviders() =>
        _entries.Where(e => e.Descriptor.IsConfigured && _isEnabled(e.Descriptor.Id))
            .Select(e => e.Provider)
            .ToList();

    public bool IsUsable(string providerId)
    {
        var descriptor = GetDescriptor(providerId);
        return descriptor is { IsConfigured: true } && _isEnabled(providerId);
    }

    private IEnumerable<ProviderEntry> UsableEntries() =>
        _entries.Where(e => e.Descriptor.IsConfigured && _isEnabled(e.Descriptor.Id));

    public bool HasCapability(string providerId, ProviderCapabilities capability) =>
        (GetDescriptor(providerId)?.Capabilities & capability) == capability;

    public IReadOnlyList<string> GetHomeSections()
    {
        var sections = new List<string>();
        foreach (var entry in UsableEntries())
        {
            foreach (var section in entry.Descriptor.HomeSections)
            {
                if (!sections.Contains(section))
                {
                    sections.Add(section);
                }
            }
        }

        return sections;
    }

    public async Task<MediaPage> GetSectionAsync(string sectionName, int page = 1, CancellationToken cancellationToken = default)
    {
        var items = new List<MediaSummary>();
        foreach (var entry in UsableEntries())
        {
            if (!entry.Descriptor.HomeSections.Contains(sectionName))
            {
                continue;
            }

            try
            {
                var result = await entry.Provider.GetSectionAsync(sectionName, page, cancellationToken).ConfigureAwait(false);
                items.AddRange(result.Items);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                // Skip providers that fail for this section.
            }
        }

        return new MediaPage(items, page, hasMore: false);
    }

    public async Task<IReadOnlyList<MediaSummary>> SearchAsync(SearchRequest request, CancellationToken cancellationToken = default)
    {
        var searches = UsableEntries()
            .Where(e => (e.Descriptor.Capabilities & ProviderCapabilities.Search) != 0)
            .Select(async entry =>
            {
                try
                {
                    return (entry, await entry.Search!.SearchAsync(request, cancellationToken).ConfigureAwait(false));
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception)
                {
                    return (entry, (IReadOnlyList<MediaSummary>)Array.Empty<MediaSummary>());
                }
            });

        var results = await Task.WhenAll(searches).ConfigureAwait(false);
        return results.SelectMany(r => r.Item2).ToList();
    }

    public async Task<MediaDetails?> GetDetailsAsync(string itemId, string providerId, CancellationToken cancellationToken = default)
    {
        var entry = _entries.FirstOrDefault(e => e.Descriptor.Id == providerId);
        if (entry is null || !entry.Descriptor.IsConfigured)
        {
            return null;
        }

        try
        {
            return await entry.Provider.GetDetailsAsync(itemId, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<SeasonInfo>?> GetSeasonsAsync(string itemId, string providerId, CancellationToken cancellationToken = default)
    {
        var entry = _entries.FirstOrDefault(e => e.Descriptor.Id == providerId);
        if (entry is null || !entry.Descriptor.IsConfigured)
        {
            return null;
        }

        try
        {
            return await entry.Provider.GetSeasonsAsync(itemId, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Route a playback resolution request to the owning provider.</summary>
    public async Task<PlaybackSource?> ResolvePlaybackAsync(string itemId, string providerId, CancellationToken cancellationToken = default)
    {
        var entry = _entries.FirstOrDefault(e => e.Descriptor.Id == providerId);
        if (entry?.Playback is null || !entry.Descriptor.IsConfigured)
        {
            return null;
        }

        try
        {
            return await entry.Playback.ResolvePlaybackAsync(itemId, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<SubtitleTrack>> ResolveSubtitlesAsync(string itemId, string providerId, CancellationToken cancellationToken = default)
    {
        var entry = _entries.FirstOrDefault(e => e.Descriptor.Id == providerId);
        if (entry?.Subtitles is null || !entry.Descriptor.IsConfigured)
        {
            return Array.Empty<SubtitleTrack>();
        }

        try
        {
            return await entry.Subtitles.GetSubtitleTracksAsync(itemId, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return Array.Empty<SubtitleTrack>();
        }
    }

    /// <summary>Route a download resolution request; null when not permitted or unavailable.</summary>
    public async Task<(PlaybackSource Source, string SuggestedFileName)?> ResolveDownloadAsync(string itemId, string providerId, CancellationToken cancellationToken = default)
    {
        var entry = _entries.FirstOrDefault(e => e.Descriptor.Id == providerId);
        if (entry?.Downloads is null || !entry.Descriptor.IsConfigured)
        {
            return null;
        }

        try
        {
            return await entry.Downloads.ResolveDownloadAsync(itemId, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<ChannelInfo>> GetLiveChannelsAsync(CancellationToken cancellationToken = default)
    {
        var all = new List<ChannelInfo>();
        foreach (var entry in UsableEntries())
        {
            if (entry.Playlists is null || (entry.Descriptor.Capabilities & ProviderCapabilities.LiveTV) == 0)
            {
                continue;
            }

            try
            {
                all.AddRange(await entry.Playlists.GetChannelsAsync(cancellationToken).ConfigureAwait(false));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                // Skip failing playlist providers.
            }
        }

        return all;
    }

    public async Task<IReadOnlyList<string>> GetLiveGroupsAsync(CancellationToken cancellationToken = default)
    {
        var groups = new List<string>();
        foreach (var entry in UsableEntries())
        {
            if (entry.Playlists is null)
            {
                continue;
            }

            try
            {
                foreach (var group in await entry.Playlists.GetGroupsAsync(cancellationToken).ConfigureAwait(false))
                {
                    if (!groups.Contains(group))
                    {
                        groups.Add(group);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                // Ignore group failures.
            }
        }

        return groups;
    }

    public async Task<ProviderTestResult> TestConnectionAsync(string providerId, CancellationToken cancellationToken = default)
    {
        var entry = _entries.FirstOrDefault(e => e.Descriptor.Id == providerId);
        if (entry is null)
        {
            return new ProviderTestResult(false, "Unknown provider.");
        }

        if (!entry.Descriptor.IsConfigured)
        {
            return new ProviderTestResult(false, $"{entry.Descriptor.DisplayName} is not configured.");
        }

        if (entry.SelfTest is null)
        {
            return new ProviderTestResult(true, "Enabled.");
        }

        try
        {
            return await entry.SelfTest.TestConnectionAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new ProviderTestResult(false, $"Connection test failed. ({ex.GetType().Name})");
        }
    }
}
