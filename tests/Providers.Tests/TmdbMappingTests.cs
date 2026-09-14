using StreamDesk.Core;
using StreamDesk.Providers.Tmdb;
using Xunit;

namespace Providers.Tests;

public class TmdbProviderTests
{
    [Fact]
    public async Task Initialize_WithoutApiKey_ReportsNotConfigured()
    {
        var provider = new TmdbProvider(() => new TmdbConfiguration());
        await provider.InitializeAsync();

        Assert.False(provider.Descriptor.IsConfigured);
        Assert.Empty(provider.Descriptor.HomeSections);
    }

    [Fact]
    public async Task Initialize_WithApiKey_ReportsConfiguredWithSections()
    {
        var provider = new TmdbProvider(() => new TmdbConfiguration { ApiKey = "test-key" });
        await provider.InitializeAsync();

        Assert.True(provider.Descriptor.IsConfigured);
        Assert.Contains("Trending", provider.Descriptor.HomeSections);
        Assert.Contains("Popular Anime", provider.Descriptor.HomeSections);
        // TMDB supplies metadata only - never playback or downloads.
        Assert.False(provider.Descriptor.Capabilities.HasFlag(ProviderCapabilities.Playback));
        Assert.False(provider.Descriptor.Capabilities.HasFlag(ProviderCapabilities.Downloads));
    }

    [Fact]
    public async Task Search_WithoutApiKey_ThrowsProviderNotConfigured()
    {
        var provider = new TmdbProvider(() => new TmdbConfiguration());
        await provider.InitializeAsync();

        await Assert.ThrowsAsync<ProviderNotConfiguredException>(
            () => provider.SearchAsync(new SearchRequest("matrix")));
    }

    [Fact]
    public async Task TestConnection_WithoutApiKey_IsHonest()
    {
        var provider = new TmdbProvider(() => new TmdbConfiguration());
        await provider.InitializeAsync();

        var result = await provider.TestConnectionAsync();
        Assert.False(result.Success);
    }
}
