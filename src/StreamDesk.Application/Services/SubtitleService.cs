using System.Collections.Generic;
using System.IO;
using System.Linq;
using StreamDesk.Core;

namespace StreamDesk.Application.Services;

/// <summary>Enumerates sidecar subtitle files next to a media file.</summary>
public sealed class SubtitleService : ISubtitleService
{
    private static readonly IReadOnlyList<string> SubtitleExtensions = new[] { ".srt", ".vtt", ".ass", ".ssa" };

    /// <summary>Extension for a subtitle track kind label.</summary>
    public static bool IsSubtitleFile(string path) =>
        SubtitleExtensions.Contains(Path.GetExtension(path)?.ToLowerInvariant());

    public IReadOnlyList<SubtitleTrack> FindLocalSubtitleTracks(string mediaPath, string preferredLanguage)
    {
        var tracks = new List<SubtitleTrack>();
        if (string.IsNullOrWhiteSpace(mediaPath) || !File.Exists(mediaPath))
        {
            return tracks;
        }

        var directory = Path.GetDirectoryName(mediaPath);
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
        {
            return tracks;
        }

        var stem = Path.GetFileNameWithoutExtension(mediaPath);
        var candidates = Directory.EnumerateFiles(directory)
            .Where(IsSubtitleFile)
            .ToList();

        // Match "Movie.srt", "Movie.en.srt", "Movie.en.hi.srt"...
        var matching = candidates.Where(f =>
        {
            var name = Path.GetFileNameWithoutExtension(f);
            return name.Equals(stem, StringComparison.OrdinalIgnoreCase)
                || name.StartsWith(stem + ".", StringComparison.OrdinalIgnoreCase);
        });

        var index = 0;
        foreach (var file in matching.OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            var language = InferLanguage(name, stem, preferredLanguage);
            tracks.Add(new SubtitleTrack(
                id: $"local:{index++}",
                language: language,
                label: Path.GetFileName(file),
                kind: SubtitleTrackKind.ExternalFile,
                location: file)
            {
                IsDefault = preferredLanguage != "auto" && language == preferredLanguage
            });
        }

        return tracks;
    }

    private static string InferLanguage(string fileName, string mediaStem, string preferredLanguage)
    {
        var suffix = fileName.Substring(mediaStem.Length).Trim('.');
        if (string.IsNullOrEmpty(suffix))
        {
            return preferredLanguage == "auto" ? "und" : preferredLanguage;
        }

        var parts = suffix.Split('.');
        return parts.FirstOrDefault(p => p.Length is 2 or 3) ?? "und";
    }
}
