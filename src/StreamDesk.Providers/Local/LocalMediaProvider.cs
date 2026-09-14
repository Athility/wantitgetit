using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StreamDesk.Application.Providers;
using StreamDesk.Core;
using StreamDesk.Infrastructure.Database;

namespace StreamDesk.Providers.Local;

/// <summary>
/// Provider over the user's own local media folders. Serves the catalog built
/// by <c>LocalLibraryService</c>; playback resolves to real local file paths.
/// </summary>
public sealed class LocalMediaProvider : IMediaProvider, ISearchProvider, IPlaybackProvider, ISelfTestProvider
{
    private static readonly ProviderDescriptor BaseDescriptor = new(
        ProviderIds.Local, "Local Library", "Your own media folders, scanned on this machine.")
    {
        Capabilities = ProviderCapabilities.Movies | ProviderCapabilities.Shows | ProviderCapabilities.Anime |
                       ProviderCapabilities.Episodes | ProviderCapabilities.Search | ProviderCapabilities.Metadata |
                       ProviderCapabilities.Playback | ProviderCapabilities.HomeSections,
        IsEnabled = true
    };

    private readonly MediaLibraryRepository _library;
    private readonly LibraryFoldersRepository _folders;

    public LocalMediaProvider(MediaLibraryRepository library, LibraryFoldersRepository folders)
    {
        _library = library;
        _folders = folders;
        Descriptor = BaseDescriptor;
    }

    public ProviderDescriptor Descriptor { get; private set; }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var hasFolders = _folders.GetAll().Any(f => f.Enabled);
        Descriptor = new ProviderDescriptor(ProviderIds.Local, BaseDescriptor.DisplayName, BaseDescriptor.Description)
        {
            Capabilities = BaseDescriptor.Capabilities,
            IsConfigured = hasFolders,
            IsEnabled = true,
            HomeSections = hasFolders
                ? new[] { "Recently Added", "Popular Movies", "Popular Shows" }
                : Array.Empty<string>()
        };
        return Task.CompletedTask;
    }

    public Task<MediaPage> GetSectionAsync(string sectionName, int page, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        var catalog = _library.GetAll();
        var items = sectionName switch
        {
            "Recently Added" => catalog.OrderByDescending(r => r.DiscoveredAtUtc),
            "Popular Movies" => catalog.Where(r => r.MediaType == MediaType.Movie).OrderBy(r => r.Title, StringComparer.OrdinalIgnoreCase),
            "Popular Shows" => catalog.Where(r => r.MediaType == MediaType.Show).OrderBy(r => r.Title, StringComparer.OrdinalIgnoreCase),
            _ => Enumerable.Empty<LibraryItemRecord>()
        };

        var summaries = items
            .Select(ToSummary)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToList();
        return Task.FromResult(new MediaPage(summaries, page, items.Count() > page * PageSize));
    }

    private const int PageSize = 60;

    public Task<MediaDetails?> GetDetailsAsync(string itemId, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        var record = FindRecord(itemId);
        if (record is null)
        {
            return Task.FromResult<MediaDetails?>(null);
        }

        var details = new MediaDetails(record.ItemId, ProviderIds.Local,
            record.MediaType, record.Title)
        {
            Year = record.Year,
            PosterUrl = record.PosterPath,
            LocalPath = record.LocalPath,
            Description = record.MediaType == MediaType.Episode ? record.Subtitle : null
        };

        if (record.MediaType == MediaType.Show)
        {
            var episodes = _library.GetAll()
                .Where(r => r.MediaType == MediaType.Episode &&
                            string.Equals(r.SeriesId, record.SeriesId, StringComparison.OrdinalIgnoreCase))
                .OrderBy(r => r.SeasonNumber).ThenBy(r => r.EpisodeNumber)
                .ToList();
            details.Seasons = episodes
                .GroupBy(e => e.SeasonNumber ?? 1)
                .Select(g => new SeasonInfo(g.Key, $"Season {g.Key}", g.Select(e => new EpisodeInfo(
                    e.ItemId, ProviderIds.Local, record.ItemId,
                    e.SeasonNumber ?? 1, e.EpisodeNumber ?? 0, CleanEpisodeTitle(e.Subtitle, e.EpisodeNumber))
                {
                    LocalPath = e.LocalPath,
                    ThumbnailUrl = null
                }).ToList()))
                .ToList();
        }

        return Task.FromResult<MediaDetails?>(details);
    }

    public Task<IReadOnlyList<SeasonInfo>?> GetSeasonsAsync(string itemId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<SeasonInfo>?>(GetDetailsAsync(itemId, cancellationToken).Result?.Seasons);
    }

    public Task<IReadOnlyList<MediaSummary>> SearchAsync(SearchRequest request, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        var term = request.Query.Trim();
        var matches = _library.GetAll()
            .Where(r => r.Title.Contains(term, StringComparison.OrdinalIgnoreCase)
                        || (r.Subtitle?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false))
            .OrderBy(r => r.Title.StartsWith(term, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(r => r.Title, StringComparer.OrdinalIgnoreCase)
            .Select(ToSummary)
            .Take(50)
            .ToList();
        return Task.FromResult<IReadOnlyList<MediaSummary>>(matches);
    }

    public Task<PlaybackSource?> ResolvePlaybackAsync(string itemId, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        var record = FindRecord(itemId);
        if (record is null || !System.IO.File.Exists(record.LocalPath))
        {
            return Task.FromResult<PlaybackSource?>(null);
        }

        return Task.FromResult<PlaybackSource?>(new PlaybackSource(PlaybackSourceKind.LocalFile, record.LocalPath)
        {
            Title = record.Subtitle is null ? record.Title : $"{record.Title} - {record.Subtitle}"
        });
    }

    public Task<ProviderTestResult> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        var folders = _folders.GetAll().Where(f => f.Enabled).ToList();
        if (folders.Count == 0)
        {
            return Task.FromResult(new ProviderTestResult(false, "No library folders configured. Add one in Settings \u2192 Local Library."));
        }

        var missing = folders.Where(f => !System.IO.Directory.Exists(f.Path)).ToList();
        return Task.FromResult(missing.Count == 0
            ? new ProviderTestResult(true, $"Local library OK ({folders.Count} folder(s)).")
            : new ProviderTestResult(false, $"{missing.Count} folder(s) no longer exist on disk."));
    }

    private LibraryItemRecord? FindRecord(string itemId) =>
        _library.GetAll().FirstOrDefault(r => r.ItemId == itemId);

    private static MediaSummary ToSummary(LibraryItemRecord record) => new(
        record.ItemId, ProviderIds.Local, record.MediaType,
        record.MediaType == MediaType.Episode ? $"{record.Title} - {record.Subtitle}" : record.Title)
    {
        Year = record.Year,
        PosterUrl = record.PosterPath,
        Category = "Local"
    };

    private static string CleanEpisodeTitle(string? subtitle, int? episodeNumber) =>
        string.IsNullOrWhiteSpace(subtitle) ? $"Episode {episodeNumber}" : subtitle;

    private void EnsureConfigured()
    {
        if (!_folders.GetAll().Any(f => f.Enabled))
        {
            throw new ProviderNotConfiguredException("Local Library");
        }
    }
}
