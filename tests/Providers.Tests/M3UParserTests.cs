using StreamDesk.Providers.M3U;
using Xunit;

namespace Providers.Tests;

public class M3UParserTests
{
    [Fact]
    public void ParseText_ExtendedPlaylist_ParsesNameGroupLogo()
    {
        var content = string.Join("\n",
            "#EXTM3U",
            "#EXTINF:-1 tvg-id=\"news.one\" tvg-name=\"News One\" tvg-logo=\"http://logo/news.png\" group-title=\"News\",News One HD",
            "http://stream.example/news.m3u8",
            "#EXTINF:-1 tvg-logo=\"http://logo/sports.png\" group-title=\"Sports\",Sports 24",
            "http://stream.example/sports.m3u8");

        var result = M3UParser.ParseText(content);

        Assert.Empty(result.Errors);
        Assert.Equal(2, result.Channels.Count);

        var first = result.Channels[0];
        Assert.Equal("News One HD", first.Name);
        Assert.Equal("News", first.Group);
        Assert.Equal("http://logo/news.png", first.LogoUrl);
        Assert.Equal("http://stream.example/news.m3u8", first.Url);

        var second = result.Channels[1];
        Assert.Equal("Sports 24", second.Name);
        Assert.Equal("Sports", second.Group);
    }

    [Fact]
    public void ParseText_MissingGroup_FallsBackToUngrouped()
    {
        var content = string.Join("\n",
            "#EXTM3U",
            "#EXTINF:-1,Plain Channel",
            "http://stream.example/plain");

        var result = M3UParser.ParseText(content);

        Assert.Single(result.Channels);
        Assert.Equal("Ungrouped", result.Channels[0].Group);
        Assert.Equal("Plain Channel", result.Channels[0].Name);
    }

    [Fact]
    public void ParseText_PlainUrlList_StillParses()
    {
        var result = M3UParser.ParseText("http://stream.example/a.m3u8\nhttp://stream.example/b.m3u8");

        Assert.Equal(2, result.Channels.Count);
        Assert.All(result.Channels, c => Assert.StartsWith("http://", c.Url));
    }

    [Fact]
    public void ParseText_EmptyContent_ReturnsError()
    {
        var result = M3UParser.ParseText("   ");
        Assert.Empty(result.Channels);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void ParseText_EntriesWithoutUrl_AreSkipped()
    {
        var content = string.Join("\n",
            "#EXTM3U",
            "#EXTINF:-1,Dangling Entry");

        var result = M3UParser.ParseText(content);

        Assert.Empty(result.Channels);
        Assert.NotEmpty(result.Errors);
    }
}
