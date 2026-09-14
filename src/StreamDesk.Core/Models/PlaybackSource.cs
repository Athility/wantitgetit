using System;
using System.Collections.Generic;

namespace StreamDesk.Core;

/// <summary>Where a playable stream comes from.</summary>
public enum PlaybackSourceKind
{
    /// <summary>A file on local disk.</summary>
    LocalFile,

    /// <summary>An HTTP(S) progressive stream, HLS/DASH manifest or user playlist entry.</summary>
    NetworkStream
}

/// <summary>
/// A concrete source the external player can open: a legitimate local file or a
/// media URL supplied by an authorized provider configuration or by the user.
/// StreamDesk never extracts unauthorized streams.
/// </summary>
public sealed class PlaybackSource
{
    public PlaybackSource()
    {
    }

    public PlaybackSource(PlaybackSourceKind kind, string location)
    {
        Kind = kind;
        Location = location;
    }

    public PlaybackSourceKind Kind { get; set; }

    /// <summary>File path (for <see cref="PlaybackSourceKind.LocalFile"/>) or URL (for network sources).</summary>
    public string Location { get; set; } = string.Empty;

    /// <summary>Human-readable title shown to the player, e.g. a window title.</summary>
    public string? Title { get; set; }

    /// <summary>Position in seconds to resume from, when known.</summary>
    public double? StartPositionSeconds { get; set; }

    /// <summary>Subtitle tracks to load alongside the source.</summary>
    public IReadOnlyList<SubtitleTrack> Subtitles { get; set; } = Array.Empty<SubtitleTrack>();

    /// <summary>
    /// Optional HTTP headers required by an authorized source (e.g. an API key
    /// header). Passed to mpv via --http-header-fields; never logged.
    /// </summary>
    public IReadOnlyDictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();
}
