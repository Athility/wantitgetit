using System;
using System.Threading;
using System.Threading.Tasks;
using StreamDesk.Application.Providers;
using StreamDesk.Core;
using StreamDesk.Providers.Shared;

namespace StreamDesk.Providers.MovieBox;

/// <summary>Configuration slot for an authorized MovieBox API connection.</summary>
public sealed class MovieBoxConfiguration
{
    /// <summary>Base URL of the API you are authorized to use (e.g. https://api.example.com/).</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>API key or token issued to you by the service operator.</summary>
    public string ApiKey { get; set; } = string.Empty;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(BaseUrl) && !string.IsNullOrWhiteSpace(ApiKey);
}

/// <summary>
/// HTTP client boundary for the authorized MovieBox API. Initially returns
/// ProviderNotConfigured/NotImplemented; plug a real client in here when you
/// have documented, authorized access to the service.
/// </summary>
public sealed class MovieBoxApiClient
{
    private readonly MovieBoxConfiguration _configuration;

    public MovieBoxApiClient(MovieBoxConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>True when a base URL and key have been supplied.</summary>
    public bool IsConfigured => _configuration.IsConfigured;

    /// <summary>
    /// IMPLEMENTATION POINT: call the authorized MovieBox API here
    /// (e.g. GET {BaseUrl}/movies?page=1 with an Authorization header).
    /// Deliberately unimplemented in StreamDesk.
    /// </summary>
    public Task<string> GetCatalogAsync(string endpoint, CancellationToken cancellationToken = default)
    {
        if (!_configuration.IsConfigured)
        {
            throw new ProviderNotConfiguredException("MovieBox");
        }

        throw new NotImplementedException("Authorized MovieBox API calls are not implemented. See docs/providers.md.");
    }
}

/// <summary>Mapper from authorized MovieBox API payloads to StreamDesk models.</summary>
public static class MovieBoxMapper
{
    // IMPLEMENTATION POINT: map the authorized API's JSON to MediaSummary,
    // MediaDetails, SeasonInfo/EpisodeInfo and PlaybackSource here.
}

/// <summary>
/// MovieBox provider adapter. Declares its intended capabilities honestly,
/// reports "Not configured" until an authorized API is supplied, and never
/// scrapes or extracts unauthorized streams.
/// </summary>
public sealed class MovieBoxProvider : AuthorizedApiProviderBase
{
    public MovieBoxProvider(Func<MovieBoxConfiguration> configuration)
        : base(
            ProviderIds.MovieBox,
            "MovieBox",
            "Adapter slot for an authorized MovieBox API. Not functional until configured.",
            AuthorizedApiProviderCapabilities.MovieBox,
            () =>
            {
                var config = configuration();
                return (config.BaseUrl, config.ApiKey);
            })
    {
        Configuration = configuration;
    }

    public Func<MovieBoxConfiguration> Configuration { get; }
}
