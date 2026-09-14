using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using StreamDesk.Application.Managers;
using StreamDesk.Application.Providers;
using StreamDesk.Core;
using StreamDesk.Infrastructure.Networking;

namespace StreamDesk.Providers.Tmdb;

/// <summary>Configuration for the TMDB metadata provider.</summary>
public sealed class TmdbConfiguration
{
    /// <summary>TMDB API (v3) key supplied by the user. Stored DPAPI-protected by the app.</summary>
    public string ApiKey { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "https://api.themoviedb.org/3";

    public string ImageBaseUrl { get; set; } = "https://image.tmdb.org/t/p";

    public string PosterSize { get; set; } = "w342";

    public string BackdropSize { get; set; } = "w1280";

    public string ProfileSize { get; set; } = "w185";

    public string Language { get; set; } = "en-US";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}

/// <summary>
/// Metadata provider for The Movie Database (TMDB). Supplies posters, backdrops,
/// descriptions, genres, cast, seasons and episodes. TMDB does not provide video
/// streams; this provider therefore declares no playback/download capability.
/// </summary>
public sealed class TmdbProvider : IMediaProvider, ISearchProvider, ISelfTestProvider
{
    public const string SectionTrending = "Trending";
    public const string SectionPopularMovies = "Popular Movies";
    public const string SectionPopularShows = "Popular Shows";
    public const string SectionPopularAnime = "Popular Anime";
    public const string SectionRecentlyAdded = "Recently Added";

    private static readonly ProviderDescriptor UnconfiguredDescriptor = new(
        ProviderIds.Tmdb, "TMDB", "Movie/show metadata and artwork from The Movie Database.")
    {
        Capabilities = ProviderCapabilities.Search | ProviderCapabilities.Metadata |
                       ProviderCapabilities.Movies | ProviderCapabilities.Shows | ProviderCapabilities.Anime |
                       ProviderCapabilities.Episodes | ProviderCapabilities.HomeSections,
        IsConfigured = false
    };

    private readonly HttpClient _httpClient;
    private readonly Func<TmdbConfiguration> _configuration;

    public TmdbProvider(Func<TmdbConfiguration> configuration, HttpClient? httpClient = null)
    {
        _configuration = configuration;
        _httpClient = httpClient ?? HttpClientFactory.Create("StreamDesk-Tmdb/1.0");
    }

    public ProviderDescriptor Descriptor { get; private set; } = UnconfiguredDescriptor;

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var config = _configuration();
        Descriptor = new ProviderDescriptor(ProviderIds.Tmdb, "TMDB", UnconfiguredDescriptor.Description)
        {
            Capabilities = UnconfiguredDescriptor.Capabilities,
            IsConfigured = config.IsConfigured,
            IsEnabled = true,
            HomeSections = config.IsConfigured
                ? new[] { SectionTrending, SectionPopularMovies, SectionPopularShows, SectionPopularAnime, SectionRecentlyAdded }
                : Array.Empty<string>()
        };
        return Task.CompletedTask;
    }

    public async Task<MediaPage> GetSectionAsync(string sectionName, int page, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        var path = sectionName switch
        {
            SectionTrending => "trending/all/week",
            SectionPopularMovies => "movie/popular",
            SectionPopularShows => "tv/popular",
            SectionPopularAnime => "discover/tv",
            SectionRecentlyAdded => "movie/now_playing",
            _ => throw new ArgumentException($"Unknown section: {sectionName}", nameof(sectionName))
        };

        var url = $"{Config.BaseUrl}/{path}?api_key={Config.ApiKey}&page={page}&language={Uri.EscapeDataString(Config.Language)}";
        if (sectionName == SectionPopularAnime)
        {
            url += "&with_genres=16&sort_by=popularity.desc";
        }

        var response = await _httpClient.GetFromJsonAsync<TmdbListResponse>(url, cancellationToken).ConfigureAwait(false);
        var items = MapSummaries(response?.Results ?? new List<TmdbListItem>());
        return new MediaPage(items, page, response?.Page < response?.TotalPages);
    }

    public async Task<IReadOnlyList<MediaSummary>> SearchAsync(SearchRequest request, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        var url = $"{Config.BaseUrl}/search/multi?api_key={Config.ApiKey}&query={Uri.EscapeDataString(request.Query)}&page={request.Page}&language={Uri.EscapeDataString(Config.Language)}";
        var response = await _httpClient.GetFromJsonAsync<TmdbListResponse>(url, cancellationToken).ConfigureAwait(false);
        return MapSummaries(response?.Results ?? new List<TmdbListItem>());
    }

    public async Task<MediaDetails?> GetDetailsAsync(string itemId, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        var (kind, id) = ParseId(itemId);
        var url = $"{Config.BaseUrl}/{(kind == "movie" ? "movie" : "tv")}/{id}?api_key={Config.ApiKey}&language={Uri.EscapeDataString(Config.Language)}&append_to_response=credits";
        var dto = await _httpClient.GetFromJsonAsync<TmdbDetailResponse>(url, cancellationToken).ConfigureAwait(false);
        return dto is null ? null : MapDetails(dto, kind);
    }

    public async Task<IReadOnlyList<SeasonInfo>?> GetSeasonsAsync(string itemId, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        var (_, id) = ParseId(itemId);
        var url = $"{Config.BaseUrl}/tv/{id}?api_key={Config.ApiKey}&language={Uri.EscapeDataString(Config.Language)}";
        var dto = await _httpClient.GetFromJsonAsync<TmdbDetailResponse>(url, cancellationToken).ConfigureAwait(false);
        if (dto?.Seasons is null)
        {
            return null;
        }

        var seasons = new List<SeasonInfo>();
        foreach (var season in dto.Seasons.Where(s => s.SeasonNumber > 0).OrderBy(s => s.SeasonNumber))
        {
            var seasonUrl = $"{Config.BaseUrl}/tv/{id}/season/{season.SeasonNumber}?api_key={Config.ApiKey}&language={Uri.EscapeDataString(Config.Language)}";
            var seasonDto = await _httpClient.GetFromJsonAsync<TmdbSeasonResponse>(seasonUrl, cancellationToken).ConfigureAwait(false);
            var episodes = (seasonDto?.Episodes ?? new List<TmdbEpisodeDto>())
                .Select(e => new EpisodeInfo(
                    $"tmdb:episode:{id}:{e.SeasonNumber}:{e.EpisodeNumber}",
                    ProviderIds.Tmdb,
                    itemId,
                    e.SeasonNumber,
                    e.EpisodeNumber,
                    e.Name ?? $"Episode {e.EpisodeNumber}")
                {
                    Description = e.Overview,
                    AirDate = ParseDate(e.AirDate),
                    DurationSeconds = e.Runtime is > 0 ? e.Runtime * 60 : null,
                    ThumbnailUrl = Image(e.StillPath, Config.PosterSize)
                })
                .ToList();
            seasons.Add(new SeasonInfo(season.SeasonNumber, season.Name ?? $"Season {season.SeasonNumber}", episodes));
        }

        return seasons;
    }

    public async Task<ProviderTestResult> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        if (!Config.IsConfigured)
        {
            return new ProviderTestResult(false, "TMDB is not configured. Add an API key in Settings \u2192 Media Providers.");
        }

        try
        {
            var url = $"{Config.BaseUrl}/configuration?api_key={Config.ApiKey}";
            using var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode
                ? new ProviderTestResult(true, "TMDB connection OK.")
                : new ProviderTestResult(false, $"TMDB returned HTTP {(int)response.StatusCode}. Check your API key.");
        }
        catch (Exception)
        {
            return new ProviderTestResult(false, "Could not reach TMDB. Check your network connection.");
        }
    }

    private TmdbConfiguration Config => _configuration();

    private void EnsureConfigured()
    {
        if (!Config.IsConfigured)
        {
            throw new ProviderNotConfiguredException("TMDB");
        }
    }

    private List<MediaSummary> MapSummaries(IEnumerable<TmdbListItem> results)
    {
        var items = new List<MediaSummary>();
        foreach (var result in results)
        {
            var isMovie = result.MediaType == "movie" || result.Title is not null;
            var title = result.Title ?? result.Name;
            if (string.IsNullOrWhiteSpace(title))
            {
                continue;
            }

            var kind = isMovie ? "movie" : "tv";
            var date = result.ReleaseDate ?? result.FirstAirDate;
            items.Add(new MediaSummary(
                $"tmdb:{kind}:{result.Id}",
                ProviderIds.Tmdb,
                isMovie ? MediaType.Movie : MediaType.Show,
                title)
            {
                Year = ParseYear(date),
                PosterUrl = Image(result.PosterPath, Config.PosterSize),
                Rating = result.VoteAverage is > 0 ? result.VoteAverage : null,
                Category = (result.OriginalLanguage == "ja" || result.OriginCountry?.Contains("JP") == true) && !isMovie ? "Anime" : null
            });
        }

        return items;
    }

    private MediaDetails MapDetails(TmdbDetailResponse dto, string kind)
    {
        var date = dto.ReleaseDate ?? dto.FirstAirDate;
        return new MediaDetails(
            $"tmdb:{kind}:{dto.Id}",
            ProviderIds.Tmdb,
            kind == "movie" ? MediaType.Movie : MediaType.Show,
            dto.Title ?? dto.Name ?? string.Empty)
        {
            OriginalTitle = dto.OriginalTitle ?? dto.OriginalName,
            Description = dto.Overview,
            Tagline = dto.Tagline,
            ReleaseDate = ParseDate(date),
            Year = ParseYear(date),
            Rating = dto.VoteAverage is > 0 ? dto.VoteAverage : null,
            RuntimeMinutes = dto.Runtime ?? dto.EpisodeRunTime?.FirstOrDefault(),
            Genres = dto.Genres?.Select(g => g.Name ?? string.Empty).ToList() ?? new List<string>(),
            Cast = dto.Credits?.Cast?.Take(12)
                .Select(c => new CastMember(c.Name ?? string.Empty, c.Character, Image(c.ProfilePath, Config.ProfileSize)))
                .ToList() ?? new List<CastMember>(),
            PosterUrl = Image(dto.PosterPath, Config.PosterSize),
            BackdropUrl = Image(dto.BackdropPath, Config.BackdropSize)
        };
    }

    private string Image(string? path, string size) =>
        string.IsNullOrWhiteSpace(path) ? string.Empty : $"{Config.ImageBaseUrl}/{size}{path}";

    private static (string Kind, string Id) ParseId(string itemId)
    {
        // Expect "tmdb:movie:603" or "tmdb:tv:1399".
        var parts = itemId.Split(':');
        if (parts.Length >= 3 && parts[0] == ProviderIds.Tmdb)
        {
            return (parts[1], parts[2]);
        }

        throw new FormatException($"Invalid TMDB item id: {itemId}");
    }

    private static int? ParseYear(string? date) =>
        ParseDate(date)?.Year;

    private static DateTime? ParseDate(string? date) =>
        DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : null;
}

#pragma warning disable CS8618 // Non-nullable field must contain a value. DTOs are populated by the serializer.
#pragma warning disable IDE1006 // Naming Styles: keep TMDB JSON names.

internal sealed class TmdbListResponse
{
    public int Page { get; set; }
    public int? TotalPages { get; set; }
    public List<TmdbListItem> Results { get; set; } = new();
}

internal sealed class TmdbListItem
{
    public long Id { get; set; }
    public string? MediaType { get; set; }
    public string? Title { get; set; }
    public string? Name { get; set; }
    public string? OriginalTitle { get; set; }
    public string? OriginalName { get; set; }
    public string? Overview { get; set; }
    public string? ReleaseDate { get; set; }
    public string? FirstAirDate { get; set; }
    public string? PosterPath { get; set; }
    public double? VoteAverage { get; set; }
    public string? OriginalLanguage { get; set; }
    public List<string>? OriginCountry { get; set; }
}

internal sealed class TmdbDetailResponse
{
    public long Id { get; set; }
    public string? Title { get; set; }
    public string? Name { get; set; }
    public string? OriginalTitle { get; set; }
    public string? OriginalName { get; set; }
    public string? Overview { get; set; }
    public string? Tagline { get; set; }
    public string? ReleaseDate { get; set; }
    public string? FirstAirDate { get; set; }
    public string? PosterPath { get; set; }
    public string? BackdropPath { get; set; }
    public double? VoteAverage { get; set; }
    public int? Runtime { get; set; }
    public List<int>? EpisodeRunTime { get; set; }
    public List<TmdbGenreDto> Genres { get; set; } = new();
    public TmdbCreditsDto? Credits { get; set; }
    public List<TmdbSeasonDto> Seasons { get; set; } = new();
}

internal sealed class TmdbGenreDto
{
    public long Id { get; set; }
    public string? Name { get; set; }
}

internal sealed class TmdbCreditsDto
{
    public List<TmdbCastDto>? Cast { get; set; }
}

internal sealed class TmdbCastDto
{
    public string? Name { get; set; }
    public string? Character { get; set; }
    public string? ProfilePath { get; set; }
}

internal sealed class TmdbSeasonDto
{
    public int SeasonNumber { get; set; }
    public string? Name { get; set; }
    public string? AirDate { get; set; }
    public int? EpisodeCount { get; set; }
}

internal sealed class TmdbSeasonResponse
{
    public List<TmdbEpisodeDto> Episodes { get; set; } = new();
}

internal sealed class TmdbEpisodeDto
{
    public int SeasonNumber { get; set; }
    public int EpisodeNumber { get; set; }
    public string? Name { get; set; }
    public string? Overview { get; set; }
    public string? AirDate { get; set; }
    public string? StillPath { get; set; }
    public int? Runtime { get; set; }
}

#pragma warning restore IDE1006
#pragma warning restore CS8618
