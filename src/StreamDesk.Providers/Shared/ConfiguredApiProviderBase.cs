using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using StreamDesk.Application.Providers;
using StreamDesk.Core;

namespace StreamDesk.Providers.Shared;

/// <summary>Capability set declared by the MovieBox and 4KHDHub adapters.</summary>
public static class AuthorizedApiProviderCapabilities
{
    public const ProviderCapabilities MovieBox =
        ProviderCapabilities.Movies | ProviderCapabilities.Shows | ProviderCapabilities.Episodes |
        ProviderCapabilities.Search | ProviderCapabilities.Metadata | ProviderCapabilities.Playback |
        ProviderCapabilities.Subtitles | ProviderCapabilities.Downloads | ProviderCapabilities.HomeSections;

    public const ProviderCapabilities FourKHDHub = MovieBox;
}

/// <summary>
/// Base class for provider adapters that require an external, authorized API
/// (base URL + credentials issued by the service operator). Until that
/// configuration is supplied, every catalog call throws
/// <see cref="ProviderNotConfiguredException"/> and the UI shows "Not configured".
///
/// THE ACTUAL API CALLS ARE DELIBERATELY NOT IMPLEMENTED. This adapter is an
/// integration point, not a scraper: implement <see cref="CreateApiClient"/> and
/// the mapper for a service you are authorized to use. No scraping, reverse
/// engineering, DRM circumvention, authentication bypass, or extraction of
/// unauthorized copyrighted streams is supported anywhere in StreamDesk.
/// </summary>
public abstract class AuthorizedApiProviderBase : IMediaProvider, ISearchProvider, IPlaybackProvider, ISubtitleProvider, IDownloadProvider, ISelfTestProvider
{
    private readonly Func<(string BaseUrl, string ApiKey)> _configuration;
    private readonly ProviderCapabilities _capabilities;

    protected AuthorizedApiProviderBase(
        string providerId,
        string displayName,
        string description,
        ProviderCapabilities capabilities,
        Func<(string BaseUrl, string ApiKey)> configuration)
    {
        _configuration = configuration;
        _capabilities = capabilities;
        Descriptor = new ProviderDescriptor(providerId, displayName, description)
        {
            Capabilities = capabilities,
            IsConfigured = false
        };
    }

    /// <summary>API client for the authorized endpoint; null until implemented.</summary>
    protected virtual object? CreateApiClient(string baseUrl, string apiKey) => null;

    public ProviderDescriptor Descriptor { get; private set; }

    protected string DisplayName => Descriptor.DisplayName;

    protected string ProviderId => Descriptor.Id;

    public virtual Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var (baseUrl, apiKey) = _configuration();
        var isConfigured = IsValidBaseUrl(baseUrl) && !string.IsNullOrWhiteSpace(apiKey);
        Descriptor = new ProviderDescriptor(Descriptor.Id, Descriptor.DisplayName, Descriptor.Description)
        {
            Capabilities = _capabilities,
            IsConfigured = isConfigured,
            IsEnabled = true
        };
        return Task.CompletedTask;
    }

    protected (string BaseUrl, string ApiKey) CurrentConfiguration() => _configuration();

    private static bool IsValidBaseUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);

    protected void EnsureConfigured()
    {
        var (baseUrl, apiKey) = _configuration();
        if (!IsValidBaseUrl(baseUrl) || string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ProviderNotConfiguredException(DisplayName);
        }

        if (CreateApiClient(baseUrl, apiKey) is null)
        {
            // Configuration present but no authorized API client has been
            // implemented yet: be honest instead of pretending to work.
            throw new ProviderException(
                $"{DisplayName} has no authorized API implementation yet. " +
                $"Provide one via {GetType().Name}.CreateApiClient (see docs/providers.md).");
        }
    }

    public Task<MediaPage> GetSectionAsync(string sectionName, int page, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        throw new NotImplementedException(
            $"Authorized API call for '{Descriptor.Id}' section '{sectionName}' is not implemented. " +
            "Implement it against a service you are authorized to use.");
    }

    public Task<MediaDetails?> GetDetailsAsync(string itemId, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        throw new NotImplementedException(
            $"Authorized API call for '{Descriptor.Id}' details is not implemented.");
    }

    public Task<IReadOnlyList<SeasonInfo>?> GetSeasonsAsync(string itemId, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        throw new NotImplementedException(
            $"Authorized API call for '{Descriptor.Id}' seasons is not implemented.");
    }

    public Task<IReadOnlyList<MediaSummary>> SearchAsync(SearchRequest request, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        throw new NotImplementedException(
            $"Authorized API call for '{Descriptor.Id}' search is not implemented.");
    }

    public Task<PlaybackSource?> ResolvePlaybackAsync(string itemId, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        throw new NotImplementedException(
            $"Authorized API call for '{Descriptor.Id}' playback is not implemented.");
    }

    public Task<IReadOnlyList<SubtitleTrack>> GetSubtitleTracksAsync(string itemId, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        throw new NotImplementedException(
            $"Authorized API call for '{Descriptor.Id}' subtitles is not implemented.");
    }

    public Task<(PlaybackSource Source, string SuggestedFileName)?> ResolveDownloadAsync(string itemId, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        throw new NotImplementedException(
            $"Authorized API call for '{Descriptor.Id}' downloads is not implemented.");
    }

    public Task<ProviderTestResult> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        var (baseUrl, apiKey) = _configuration();
        if (!IsValidBaseUrl(baseUrl) || string.IsNullOrWhiteSpace(apiKey))
        {
            return Task.FromResult(new ProviderTestResult(false, $"{DisplayName} is not configured."));
        }

        // Configuration is present, but no authorized API client exists yet.
        return Task.FromResult(new ProviderTestResult(
            false,
            $"{DisplayName} needs an authorized API implementation before it can connect. See docs/providers.md."));
    }
}
