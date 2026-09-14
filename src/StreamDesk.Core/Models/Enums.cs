namespace StreamDesk.Core;

/// <summary>Application theme preference.</summary>
public enum ThemeMode
{
    /// <summary>Follow the operating system theme.</summary>
    System,

    /// <summary>Always dark.</summary>
    Dark,

    /// <summary>Always light.</summary>
    Light
}

/// <summary>External player preference.</summary>
public enum PlayerKind
{
    /// <summary>Try mpv, then VLC, then default OS handler.</summary>
    AutoDetect,

    /// <summary>Always use VLC.</summary>
    Vlc,

    /// <summary>Always use mpv.</summary>
    Mpv
}

/// <summary>Download lifecycle states.</summary>
public enum DownloadStatus
{
    Queued,
    Downloading,
    Paused,
    Completed,
    Failed,
    Cancelled
}
