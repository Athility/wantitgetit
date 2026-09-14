using StreamDesk.Core;
using Xunit;

namespace Core.Tests;

/// <summary>Core model and contract tests with no infrastructure dependencies.</summary>
public class CoreModelTests
{
    [Fact]
    public void MediaDetails_Defaults_AreSafeForUiBinding()
    {
        var details = new MediaDetails();

        Assert.Empty(details.Genres);
        Assert.Empty(details.Cast);
        Assert.Empty(details.Seasons);
        Assert.Equal(string.Empty, details.Title);
    }

    [Fact]
    public void MediaSummary_QualifiedId_IsStable()
    {
        var summary = new MediaSummary("tmdb:movie:603", "tmdb", MediaType.Movie, "The Matrix")
        {
            Year = 1999,
            Rating = 8.4
        };

        Assert.Equal(MediaType.Movie, summary.MediaType);
        Assert.Equal(1999, summary.Year);
        Assert.Equal(8.4, summary.Rating);
    }

    [Fact]
    public void ProviderCapabilities_Flags_Combine()
    {
        var caps = ProviderCapabilities.Movies | ProviderCapabilities.Search | ProviderCapabilities.Playback;

        Assert.True(caps.HasFlag(ProviderCapabilities.Movies));
        Assert.True(caps.HasFlag(ProviderCapabilities.Search));
        Assert.False(caps.HasFlag(ProviderCapabilities.LiveTV));
    }

    [Fact]
    public void ProviderNotConfiguredException_IncludesProviderName()
    {
        var ex = new ProviderNotConfiguredException("MovieBox");

        Assert.Contains("MovieBox", ex.Message);
        Assert.Contains("not configured", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PlaybackSource_KindAndLocation_Roundtrip()
    {
        var source = new PlaybackSource(PlaybackSourceKind.LocalFile, @"C:\media\movie.mkv");

        Assert.Equal(PlaybackSourceKind.LocalFile, source.Kind);
        Assert.Equal(@"C:\media\movie.mkv", source.Location);
    }
}
