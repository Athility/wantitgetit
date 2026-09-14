namespace StreamDesk.Core;

/// <summary>
/// Application settings. Persisted as JSON by the Infrastructure layer; never
/// contains secrets (API keys live in provider configuration, not here).
/// </summary>
public sealed class AppSettings
{
    public const string CurrentVersion = "1.0.0";

    public string SettingsVersion { get; set; } = "1";

    // Appearance
    public ThemeMode Theme { get; set; } = ThemeMode.System;

    // Playback
    public PlayerKind PreferredPlayer { get; set; } = PlayerKind.AutoDetect;

    /// <summary>Explicit path to vlc.exe; empty = auto-detect.</summary>
    public string VlcPath { get; set; } = string.Empty;

    /// <summary>Explicit path to mpv.exe; empty = auto-detect.</summary>
    public string MpvPath { get; set; } = string.Empty;

    public bool AutoPlayNextEpisode { get; set; } = true;

    // Subtitles
    /// <summary>ISO code like "en" / "hi", or "auto".</summary>
    public string PreferredSubtitleLanguage { get; set; } = "auto";

    public bool AutoSelectSubtitles { get; set; } = true;

    // Downloads
    public string DownloadDirectory { get; set; } = string.Empty;

    public int ConcurrentDownloads { get; set; } = 2;

    public bool ResumeDownloads { get; set; } = true;

    // Live TV
    public string? SelectedLiveTvPlaylistId { get; set; }

    // Providers
    public bool ProviderEnabled_Tmdb { get; set; } = true;

    public bool ProviderEnabled_Local { get; set; } = true;

    public bool ProviderEnabled_M3U { get; set; } = true;

    public bool ProviderEnabled_MovieBox { get; set; }

    public bool ProviderEnabled_FourKHDHub { get; set; }

    public string TmdbApiKey { get; set; } = string.Empty;

    /// <summary>Authorized API base URL for the MovieBox adapter; empty until supplied.</summary>
    public string MovieBoxBaseUrl { get; set; } = string.Empty;

    /// <summary>API key/token issued for the authorized MovieBox API; empty until supplied.</summary>
    public string MovieBoxApiKey { get; set; } = string.Empty;

    /// <summary>Authorized API base URL for the 4KHDHub adapter; empty until supplied.</summary>
    public string FourKHDHubBaseUrl { get; set; } = string.Empty;

    /// <summary>API key/token issued for the authorized 4KHDHub API; empty until supplied.</summary>
    public string FourKHDHubApiKey { get; set; } = string.Empty;

    // Application
    public string GitHubRepository { get; set; } = "https://github.com/streamdesk/streamdesk";
}

/// <summary>A favorited item (movie, show, episode or live channel).</summary>
public sealed class FavoriteItem
{
    public string ItemId { get; set; } = string.Empty;

    public string ProviderId { get; set; } = string.Empty;

    public MediaType MediaType { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? PosterUrl { get; set; }

    public int? Year { get; set; }

    public double? Rating { get; set; }

    public string? Category { get; set; }

    public DateTime AddedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>Watch history / playback progress entry.</summary>
public sealed class WatchHistoryEntry
{
    public string ItemId { get; set; } = string.Empty;

    public string ProviderId { get; set; } = string.Empty;

    public MediaType MediaType { get; set; }

    /// <summary>Series id for episodes; empty for movies/channels.</summary>
    public string SeriesId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    /// <summary>Sub-label for episodes, e.g. "S1 E4 - Title".</summary>
    public string? Subtitle { get; set; }

    public string? PosterUrl { get; set; }

    public int? SeasonNumber { get; set; }

    public int? EpisodeNumber { get; set; }

    public double PositionSeconds { get; set; }

    public double? DurationSeconds { get; set; }

    public bool Completed { get; set; }

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Completion percentage 0-100 (when duration known).</summary>
    public double PercentComplete => DurationSeconds is > 0 ? Math.Min(100, PositionSeconds / DurationSeconds.Value * 100) : 0;
}

/// <summary>A user-imported M3U/M3U8 playlist source.</summary>
public sealed class PlaylistSource
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = string.Empty;

    /// <summary>True when <see cref="Url"/> is used; false when <see cref="FilePath"/> is used.</summary>
    public bool IsRemote { get; set; }

    /// <summary>Remote playlist URL (m3u/m3u8).</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Local playlist file path.</summary>
    public string FilePath { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;

    public DateTime? LastRefreshedUtc { get; set; }
}

/// <summary>A local media library folder registered by the user.</summary>
public sealed class LibraryFolder
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Path { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;
}
