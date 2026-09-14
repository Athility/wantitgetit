using System;
using System.Threading;
using System.Threading.Tasks;
using StreamDesk.Application.Providers;
using StreamDesk.Core;
using StreamDesk.Providers.Shared;

namespace StreamDesk.Providers.FourKHDHub;

/// <summary>Configuration slot for an authorized 4KHDHub API connection.</summary>
public sealed class FourKHDHubConfiguration
{
    /// <summary>Base URL of the API you are authorized to use.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>API key or token issued to you by the service operator.</summary>
    public string ApiKey { get; set; } = string.Empty;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(BaseUrl) && !string.IsNullOrWhiteSpace(ApiKey);
}

/// <summary>
/// HTTP client boundary for the authorized 4KHDHub API. Initially returns
/// ProviderNotConfigured/NotImplemented; plug a real client in here when you
/// have documented, authorized access to the service.
/// </summary>
public sealed class FourKHDHubApiClient
{
    private readonly FourKHDHubConfiguration _configuration;

    public FourKHDHubApiClient(FourKHDHubConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>True when a base URL and key have been supplied.</summary>
    public bool IsConfigured => _configuration.IsConfigured;

    /// <summary>
    /// IMPLEMENTATION POINT: call the authorized 4KHDHub API here.
    /// Deliberately unimplemented in StreamDesk.
    /// </summary>
    public Task<string> GetCatalogAsync(string endpoint, CancellationToken cancellationToken = default)
    {
        if (!_configuration.IsConfigured)
        {
            throw new ProviderNotConfiguredException("4KHDHub");
        }

        throw new NotImplementedException("Authorized 4KHDHub API calls are not implemented. See docs/providers.md.");
    }
}

/// <summary>Mapper from authorized 4KHDHub API payloads to StreamDesk models.</summary>
public static class FourKHDHubMapper
{
    // IMPLEMENTATION POINT: map the authorized API's JSON to MediaSummary,
    // MediaDetails, SeasonInfo/EpisodeInfo and PlaybackSource here.
}

/// <summary>
/// 4KHDHub provider adapter. Declares its intended capabilities honestly,
/// reports "Not configured" until an authorized API is supplied, and never
/// scrapes or extracts unauthorized streams.
/// </summary>
public sealed class FourKHDHubProvider : AuthorizedApiProviderBase
{
    public FourKHDHubProvider(Func<FourKHDHubConfiguration> configuration)
        : base(
            ProviderIds.FourKHDHub,
            "4KHDHub",
            "Adapter slot for an authorized 4KHDHub API. Not functional until configured.",
            AuthorizedApiProviderCapabilities.FourKHDHub,
            () =>
            {
                var config = configuration();
                return (config.BaseUrl, config.ApiKey);
            })
    {
        Configuration = configuration;
    }

    public Func<FourKHDHubConfiguration> Configuration { get; }
}
