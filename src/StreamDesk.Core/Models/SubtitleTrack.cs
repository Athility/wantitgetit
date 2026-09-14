namespace StreamDesk.Core;

/// <summary>How a subtitle track is delivered.</summary>
public enum SubtitleTrackKind
{
    /// <summary>Embedded in the container and handled by the player.</summary>
    Embedded,

    /// <summary>A separate sidecar file (srt/vtt/ass) passed to the player.</summary>
    ExternalFile,

    /// <summary>A remote sidecar subtitle (URL).</summary>
    ExternalUrl
}

/// <summary>A subtitle track offered for playback.</summary>
public sealed class SubtitleTrack
{
    public SubtitleTrack()
    {
    }

    public SubtitleTrack(string id, string language, string label, SubtitleTrackKind kind, string location)
    {
        Id = id;
        Language = language;
        Label = label;
        Kind = kind;
        Location = location;
    }

    public string Id { get; set; } = string.Empty;

    /// <summary>ISO language code, e.g. "en", "hi"; "und" when unknown.</summary>
    public string Language { get; set; } = "und";

    public string Label { get; set; } = string.Empty;

    public SubtitleTrackKind Kind { get; set; }

    /// <summary>File path or URL depending on <see cref="Kind"/>.</summary>
    public string Location { get; set; } = string.Empty;

    /// <summary>True when this track is the user's preferred language.</summary>
    public bool IsDefault { get; set; }
}
