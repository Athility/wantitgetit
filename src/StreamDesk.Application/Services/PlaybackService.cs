using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StreamDesk.Core;

namespace StreamDesk.Application.Services;

/// <summary>
/// Launches legitimate local files or authorized network sources in VLC or mpv.
/// Detection order for AutoDetect: mpv, then VLC, then the OS default handler.
/// </summary>
public sealed class PlaybackService : IPlaybackService
{
    /// <summary>Common Windows install locations checked during auto-detection.</summary>
    public static readonly IReadOnlyList<string> VlcSearchPaths = new[]
    {
        @"C:\Program Files\VideoLAN\VLC\vlc.exe",
        @"C:\Program Files (x86)\VideoLAN\VLC\vlc.exe"
    };

    public static readonly IReadOnlyList<string> MpvSearchPaths = new[]
    {
        @"C:\Program Files\mpv\mpv.exe",
        @"C:\Program Files (x86)\mpv\mpv.exe",
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "mpv", "mpv.exe")
    };

    private readonly ISettingsService _settings;

    public PlaybackService(ISettingsService settings)
    {
        _settings = settings;
    }

    public IReadOnlyDictionary<PlayerKind, string?> DetectPlayers()
    {
        return new Dictionary<PlayerKind, string?>
        {
            [PlayerKind.Vlc] = FindExecutable(_settings.Current.VlcPath, VlcSearchPaths),
            [PlayerKind.Mpv] = FindExecutable(_settings.Current.MpvPath, MpvSearchPaths)
        };
    }

    public bool IsPlayerAvailable(PlayerKind kind)
    {
        var detected = DetectPlayers();
        return kind switch
        {
            PlayerKind.Vlc => detected[PlayerKind.Vlc] is not null,
            PlayerKind.Mpv => detected[PlayerKind.Mpv] is not null,
            PlayerKind.AutoDetect => detected.Values.Any(p => p is not null),
            _ => false
        };
    }

    public Task<string?> PlayAsync(PlaybackSource source, PlayerKind? overridePlayer = null, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(source?.Location))
            {
                return Task.FromResult<string?>("Unable to play this item.\n\nThe media source is empty or invalid.");
            }

            if (source.Kind == PlaybackSourceKind.LocalFile && !File.Exists(source.Location))
            {
                return Task.FromResult<string?>("Unable to play this item.\n\nThe file no longer exists on disk.");
            }

            var player = overridePlayer ?? _settings.Current.PreferredPlayer;
            var detected = DetectPlayers();

            if (player == PlayerKind.AutoDetect)
            {
                if (detected[PlayerKind.Mpv] is { } mpv)
                {
                    return Task.FromResult(LaunchMpv(mpv, source));
                }

                if (detected[PlayerKind.Vlc] is { } vlc)
                {
                    return Task.FromResult(LaunchVlc(vlc, source));
                }

                return Task.FromResult(LaunchWithDefaultHandler(source));
            }

            var exe = detected[player];
            if (exe is null)
            {
                var friendly = player == PlayerKind.Vlc ? "VLC" : "mpv";
                return Task.FromResult<string?>($"Unable to play this item.\n\n{friendly} was not found. Install it or set its path in Settings \u2192 Playback.");
            }

            return Task.FromResult(player == PlayerKind.Vlc ? LaunchVlc(exe, source) : LaunchMpv(exe, source));
        }
        catch (Exception ex)
        {
            return Task.FromResult<string?>($"Unable to play this item.\n\nThe player failed to start. ({ex.GetType().Name})");
        }
    }

    /// <summary>Resolves the player kind the service would currently use.</summary>
    public static PlayerKind ResolvePreferredPlayer(ISettingsService settings, PlayerKind? overridePlayer) =>
        overridePlayer ?? settings.Current.PreferredPlayer;

    private static string? FindExecutable(string? configuredPath, IReadOnlyList<string> searchPaths)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return File.Exists(configuredPath) ? configuredPath : null;
        }

        return searchPaths.FirstOrDefault(File.Exists);
    }

    private static string? LaunchVlc(string exe, PlaybackSource source)
    {
        var startInfo = new ProcessStartInfo(exe) { UseShellExecute = true };
        var args = startInfo.ArgumentList;
        args.Add(source.Location);
        args.Add("--started-from-file");

        if (source.StartPositionSeconds is > 0)
        {
            args.Add($"--start-time={(long)source.StartPositionSeconds.Value}");
        }

        foreach (var subtitle in source.Subtitles.Where(s => s.Kind != SubtitleTrackKind.Embedded))
        {
            args.Add($":sub-file={subtitle.Location}");
        }

        if (!string.IsNullOrWhiteSpace(source.Title))
        {
            args.Add($"--meta-title={source.Title}");
        }

        return Process.Start(startInfo) is null
            ? "Unable to play this item.\n\nVLC failed to start."
            : null;
    }

    private static string? LaunchMpv(string exe, PlaybackSource source)
    {
        var startInfo = new ProcessStartInfo(exe) { UseShellExecute = true };
        var args = startInfo.ArgumentList;
        args.Add(source.Location);
        args.Add("--keep-open=no");

        if (source.StartPositionSeconds is > 0)
        {
            args.Add($"--start={FormatTime(source.StartPositionSeconds.Value)}");
        }

        if (!string.IsNullOrWhiteSpace(source.Title))
        {
            args.Add($"--title={source.Title}");
        }

        var sidecars = source.Subtitles
            .Where(s => s.Kind is SubtitleTrackKind.ExternalFile or SubtitleTrackKind.ExternalUrl)
            .Select(s => s.Location)
            .ToList();
        if (sidecars.Count > 0)
        {
            args.Add("--sub-files=" + string.Join(",", sidecars));
        }

        foreach (var header in source.Headers)
        {
            args.Add($"--http-header-fields={header.Key}: {header.Value}");
        }

        return Process.Start(startInfo) is null
            ? "Unable to play this item.\n\nmpv failed to start."
            : null;
    }

    private static string? LaunchWithDefaultHandler(PlaybackSource source)
    {
        try
        {
            Process.Start(new ProcessStartInfo(source.Location) { UseShellExecute = true });
            return null;
        }
        catch (Exception)
        {
            return "Unable to play this item.\n\nNo suitable player was found. Install VLC or mpv and set it in Settings \u2192 Playback.";
        }
    }

    private static string FormatTime(double seconds)
    {
        var span = TimeSpan.FromSeconds(seconds);
        return span.ToString(span.TotalHours >= 1 ? @"hh\:mm\:ss" : @"mm\:ss");
    }
}
