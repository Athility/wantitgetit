using System;
using System.Collections.Generic;
using System.IO;

namespace StreamDesk.Providers.M3U;

/// <summary>Result of parsing an M3U playlist.</summary>
public sealed class M3UParseResult
{
    public List<M3UChannel> Channels { get; } = new();

    public List<string> Errors { get; } = new();
}

/// <summary>One playlist entry.</summary>
public sealed class M3UChannel
{
    public string Name { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public string Group { get; set; } = "Ungrouped";

    public string? LogoUrl { get; set; }

    public double? Duration { get; set; }
}

/// <summary>
/// Parser for IPTV-style extended M3U/M3U8 playlists (#EXTM3U, #EXTINF with
/// tvg-name/tvg-logo/group-title attributes) and plain M3U URL lists.
/// </summary>
public static class M3UParser
{
    public static M3UParseResult ParseText(string content)
    {
        var result = new M3UParseResult();
        if (string.IsNullOrWhiteSpace(content))
        {
            result.Errors.Add("The playlist is empty.");
            return result;
        }

        var lines = content.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
        M3UChannel? current = null;
        var index = 0;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (line.StartsWith("#EXTM3U", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (line.StartsWith("#EXTINF:", StringComparison.OrdinalIgnoreCase))
            {
                current = ParseExtInf(line, index++);
                continue;
            }

            if (line.StartsWith("#", StringComparison.Ordinal))
            {
                // Other directives (#EXTVLCOPT, #KODIPROP...) are ignored safely.
                continue;
            }

            if (current is null)
            {
                // Plain M3U without EXTINF entries: treat the line as a bare URL.
                if (Uri.TryCreate(line, UriKind.Absolute, out var uri) &&
                    (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeFile))
                {
                    var fileName = Path.GetFileName(uri.LocalPath);
                    result.Channels.Add(new M3UChannel
                    {
                        Name = fileName.Length > 0 ? fileName : uri.AbsoluteUri,
                        Url = line
                    });
                    index++;
                }
                else
                {
                    result.Errors.Add($"Skipped unrecognizable line: {Truncate(line)}");
                }

                continue;
            }

            current.Url = line;
            result.Channels.Add(current);
            current = null;
        }

        if (current is not null)
        {
            result.Errors.Add("The last channel entry has no URL and was skipped.");
        }

        return result;
    }

    public static M3UParseResult ParseFile(string path)
    {
        try
        {
            return ParseText(File.ReadAllText(path));
        }
        catch (Exception ex)
        {
            var failure = new M3UParseResult();
            failure.Errors.Add($"Could not read the playlist file. ({ex.GetType().Name})");
            return failure;
        }
    }

    private static M3UChannel ParseExtInf(string line, int index)
    {
        var channel = new M3UChannel();
        var colon = line.IndexOf(':');
        var comma = line.IndexOf(',', colon + 1);

        if (comma < 0)
        {
            return new M3UChannel { Name = "Channel " + (index + 1) };
        }

        var attrs = line.Substring(colon + 1, comma - colon - 1);
        var durationText = comma - colon - 1 > 0 ? attrs.Substring(0, attrs.IndexOf(',') is var c && c >= 0 && c < attrs.Length ? c : attrs.Length) : string.Empty;
        var durationEnd = attrs.IndexOf(',');
        if (durationEnd < 0)
        {
            durationEnd = attrs.Length;
        }

        channel.Duration = double.TryParse(attrs.AsSpan(0, durationEnd).Trim(), out var duration) ? duration : null;
        channel.Name = line[(comma + 1)..].Trim();
        channel.LogoUrl = ExtractAttribute(attrs, "tvg-logo") ?? ExtractAttribute(attrs, "logo");

        var group = ExtractAttribute(attrs, "group-title");
        channel.Group = string.IsNullOrWhiteSpace(group) ? "Ungrouped" : group.Trim();

        var tvgName = ExtractAttribute(attrs, "tvg-name");
        if (string.IsNullOrWhiteSpace(channel.Name) && !string.IsNullOrWhiteSpace(tvgName))
        {
            channel.Name = tvgName;
        }

        if (string.IsNullOrWhiteSpace(channel.Name))
        {
            channel.Name = "Channel " + (index + 1);
        }

        return channel;
    }

    private static string? ExtractAttribute(string attrs, string name)
    {
        var pattern = name + "=\"";
        var start = attrs.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            return null;
        }

        start += pattern.Length;
        var end = attrs.IndexOf('"', start);
        return end < 0 ? null : attrs[start..end];
    }

    private static string Truncate(string value) =>
        value.Length <= 60 ? value : value[..60] + "\u2026";
}
