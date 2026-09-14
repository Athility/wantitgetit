using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using StreamDesk.Core;

namespace StreamDesk.Application.Services;

/// <summary>Reads/writes <see cref="AppSettings"/> and raises change notifications.</summary>
public interface ISettingsService
{
    AppSettings Current { get; }

    event Action? Changed;

    void Load();

    void Save();
}

/// <summary>Favorites across movies, shows, episodes and channels.</summary>
public interface IFavoritesService
{
    event Action? Changed;

    IReadOnlyList<FavoriteItem> GetAll();

    bool IsFavorite(string providerId, string itemId);

    void Toggle(MediaSummary summary);

    void ToggleChannel(ChannelInfo channel);

    void Remove(string providerId, string itemId);
}

/// <summary>Watch history and playback positions.</summary>
public interface IWatchHistoryService
{
    event Action? Changed;

    /// <summary>Most recent items with progress, newest first.</summary>
    IReadOnlyList<WatchHistoryEntry> GetContinueWatching(int limit = 20);

    WatchHistoryEntry? Get(string providerId, string itemId);

    /// <summary>Record progress after playback or from a player report.</summary>
    void RecordProgress(WatchHistoryEntry entry);

    void Remove(string providerId, string itemId);
}

/// <summary>Launches sources in an external player (VLC/mpv).</summary>
public interface IPlaybackService
{
    /// <summary>Resolved executable paths by player kind.</summary>
    IReadOnlyDictionary<PlayerKind, string?> DetectPlayers();

    /// <summary>True when the given player (or any, for AutoDetect) is available.</summary>
    bool IsPlayerAvailable(PlayerKind kind);

    /// <summary>Launch the source; returns a friendly error message on failure, null on success.</summary>
    Task<string?> PlayAsync(PlaybackSource source, PlayerKind? overridePlayer = null, CancellationToken cancellationToken = default);
}

/// <summary>Enumerates sidecar and provider subtitle tracks for a local file.</summary>
public interface ISubtitleService
{
    /// <summary>Sidecar tracks found next to a local media file (srt/vtt/ass).</summary>
    IReadOnlyList<SubtitleTrack> FindLocalSubtitleTracks(string mediaPath, string preferredLanguage);
}
