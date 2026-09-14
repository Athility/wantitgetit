using StreamDesk.Core;
using StreamDesk.Providers.FourKHDHub;
using StreamDesk.Providers.MovieBox;
using Xunit;

namespace Providers.Tests;

/// <summary>
/// Contract tests: unconfigured provider stubs must fail honestly and never
/// return fake successful responses.
/// </summary>
public class UnconfiguredProviderTests
{
    private static MovieBoxProvider NewMovieBox() =>
        new(() => new MovieBoxConfiguration());

    private static FourKHDHubProvider NewFourK() =>
        new(() => new FourKHDHubConfiguration());

    [Fact]
    public async Task MovieBox_Initialize_ReportsNotConfigured()
    {
        var provider = NewMovieBox();
        await provider.InitializeAsync();

        Assert.False(provider.Descriptor.IsConfigured);
        Assert.False(string.IsNullOrEmpty(provider.Descriptor.DisplayName));
    }

    [Fact]
    public async Task MovieBox_Search_ThrowsProviderNotConfigured()
    {
        var provider = NewMovieBox();
        await provider.InitializeAsync();

        await Assert.ThrowsAsync<ProviderNotConfiguredException>(
            () => provider.SearchAsync(new SearchRequest("query")));
    }

    [Fact]
    public async Task MovieBox_Details_ThrowsProviderNotConfigured()
    {
        var provider = NewMovieBox();
        await provider.InitializeAsync();

        await Assert.ThrowsAsync<ProviderNotConfiguredException>(
            () => provider.GetDetailsAsync("moviebox:movie:1"));
    }

    [Fact]
    public async Task MovieBox_Playback_ThrowsProviderNotConfigured()
    {
        var provider = NewMovieBox();
        await provider.InitializeAsync();

        await Assert.ThrowsAsync<ProviderNotConfiguredException>(
            () => provider.ResolvePlaybackAsync("moviebox:movie:1"));
    }

    [Fact]
    public async Task MovieBox_TestConnection_IsHonest()
    {
        var provider = NewMovieBox();
        await provider.InitializeAsync();

        var result = await provider.TestConnectionAsync();
        Assert.False(result.Success);
        Assert.Contains("not configured", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FourKHDHub_Initialize_ReportsNotConfigured()
    {
        var provider = NewFourK();
        await provider.InitializeAsync();

        Assert.False(provider.Descriptor.IsConfigured);
    }

    [Fact]
    public async Task FourKHDHub_Search_ThrowsProviderNotConfigured()
    {
        var provider = NewFourK();
        await provider.InitializeAsync();

        await Assert.ThrowsAsync<ProviderNotConfiguredException>(
            () => provider.SearchAsync(new SearchRequest("query")));
    }

    [Fact]
    public async Task FourKHDHub_Download_ThrowsProviderNotConfigured()
    {
        var provider = NewFourK();
        await provider.InitializeAsync();

        await Assert.ThrowsAsync<ProviderNotConfiguredException>(
            () => provider.ResolveDownloadAsync("4khdhub:movie:1"));
    }

    [Fact]
    public async Task FourKHDHub_TestConnection_IsHonest()
    {
        var provider = NewFourK();
        await provider.InitializeAsync();

        var result = await provider.TestConnectionAsync();
        Assert.False(result.Success);
        Assert.Contains("not configured", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MovieBox_DeclaresIntendedCapabilities()
    {
        var provider = NewMovieBox();
        Assert.True(provider.Descriptor.Capabilities.HasFlag(ProviderCapabilities.Movies));
        Assert.True(provider.Descriptor.Capabilities.HasFlag(ProviderCapabilities.Subtitles));
        Assert.True(provider.Descriptor.Capabilities.HasFlag(ProviderCapabilities.Downloads));
    }

    [Fact]
    public async Task ConfiguredButUnimplemented_Client_ThrowsNotImplementedNotFake()
    {
        // With base URL + key supplied, the adapter reports configured, but the
        // catalog calls refuse to fake success: they throw a clear provider error
        // naming the exact extension point for an authorized implementation.
        var provider = new MovieBoxProvider(() => new MovieBoxConfiguration
        {
            BaseUrl = "https://api.example.com",
            ApiKey = "test-key-issued-by-operator"
        });
        await provider.InitializeAsync();

        Assert.True(provider.Descriptor.IsConfigured);
        var ex = await Assert.ThrowsAsync<ProviderException>(
            () => provider.SearchAsync(new SearchRequest("q")));
        Assert.Contains("no authorized API implementation", ex.Message);
        Assert.Contains("CreateApiClient", ex.Message);
    }
}
