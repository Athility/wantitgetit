using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using StreamDesk.Core;
using StreamDesk.Infrastructure.Database;

namespace StreamDesk.Application.Services;

/// <summary>Result of scanning library folders.</summary>
public sealed record LibraryScanResult(int FilesAdded, int FilesRemoved, IReadOnlyList<string> Errors)
{
    public bool Success => Errors.Count == 0;
}

/// <summary>
/// Scans user-registered folders and builds a catalog of local movies, shows
/// and anime using file naming conventions. Metadata stays local; no network.
/// </summary>
public sealed partial class LocalLibraryService
{
    /// <summary>Formats the playback engine and local provider can open.</summary>
    public static readonly IReadOnlyList<string> VideoExtensions = new[]
    {
        ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".m4v", ".ts", ".webm", ".mpg", ".mpeg", ".flv"
    };

    private static readonly Regex SeasonEpisodeRegex = CreateSeasonEpisodeRegex();
    private static readonly Regex YearRegex = CreateYearRegex();

    private readonly LibraryFoldersRepository _folders;
    private readonly MediaLibraryRepository _library;

    public LocalLibraryService(LibraryFoldersRepository folders, MediaLibraryRepository library)
    {
        _folders = folders;
        _library = library;
    }

    public IReadOnlyList<LibraryFolder> GetFolders() => _folders.GetAll();

    public LibraryFolder AddFolder(string path)
    {
        var full = System.IO.Path.GetFullPath(path);
        if (!Directory.Exists(full))
        {
            throw new DirectoryNotFoundException($"Folder not found: {full}");
        }

        var folder = new LibraryFolder { Path = full };
        _folders.Upsert(folder);
        return folder;
    }

    public void RemoveFolder(string folderId) => _folders.Remove(folderId);

    public LibraryScanResult ScanAll()
    {
        var added = 0;
        var errors = new List<string>();
        var before = _library.GetAll().Count;

        foreach (var folder in _folders.GetAll().Where(f => f.Enabled))
        {
            try
            {
                var items = ScanFolder(folder);
                _library.ReplaceForFolder(folder.Id, items);
                added += items.Count;
            }
            catch (Exception ex)
            {
                errors.Add($"{folder.Path}: {ex.Message}");
            }
        }

        var after = _library.GetAll().Count;
        return new LibraryScanResult(added, Math.Max(0, before - after), errors);
    }

    /// <summary>Scan one folder into library records using naming conventions.</summary>
    public IReadOnlyList<LibraryItemRecord> ScanFolder(LibraryFolder folder)
    {
        var records = new List<LibraryItemRecord>();
        foreach (var file in Directory.EnumerateFiles(folder.Path, "*", SearchOption.AllDirectories))
        {
            if (!VideoExtensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
            {
                continue;
            }

            var fileInfo = new FileInfo(file);
            if (fileInfo.DirectoryName is null)
            {
                continue;
            }

            var fileName = Path.GetFileNameWithoutExtension(file);
            var relative = Path.GetRelativePath(folder.Path, file);
            var parts = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            var seasonEpisode = SeasonEpisodeRegex.Match(fileName);
            var year = MatchYear(fileName);

            if (seasonEpisode.Success)
            {
                // Show directory (or fallback: file's own directory) is the series name.
                var seriesDir = parts.Length >= 2 ? parts[^2] : fileInfo.Directory!.Name;
                var seriesName = CleanName(seriesDir);
                var record = new LibraryItemRecord
                {
                    ItemId = $"ep:{fileInfo.FullName.ToLowerInvariant().GetHashCode():x8}",
                    ProviderId = folder.Id,
                    MediaType = MediaType.Episode,
                    SeriesId = seriesName,
                    Title = seriesName,
                    Subtitle = $"S{seasonEpisode.Groups["season"].Value} E{seasonEpisode.Groups["episode"].Value} - {CleanName(seasonEpisode.Groups["title"].Value)}",
                    Year = year,
                    LocalPath = file,
                    SeasonNumber = int.Parse(seasonEpisode.Groups["season"].Value),
                    EpisodeNumber = int.Parse(seasonEpisode.Groups["episode"].Value),
                    FileSizeBytes = fileInfo.Length
                };
                records.Add(record);
                records.Add(BuildSeriesRecord(folder.Id, records, record, seriesName));
            }
            else
            {
                records.Add(new LibraryItemRecord
                {
                    ItemId = $"mv:{fileInfo.FullName.ToLowerInvariant().GetHashCode():x8}",
                    ProviderId = folder.Id,
                    MediaType = MediaType.Movie,
                    Title = CleanName(fileName),
                    Year = year,
                    LocalPath = file,
                    FileSizeBytes = fileInfo.Length
                });
            }
        }

        return records;
    }

    private LibraryItemRecord BuildSeriesRecord(string folderId, List<LibraryItemRecord> records, LibraryItemRecord episode, string seriesName)
    {
        var seriesId = $"series:{seriesName.ToLowerInvariant()}";
        var existing = records.FirstOrDefault(r => r.ItemId == seriesId && r.ProviderId == folderId);
        if (existing is null)
        {
            existing = new LibraryItemRecord
            {
                ItemId = seriesId,
                ProviderId = folderId,
                MediaType = MediaType.Show,
                SeriesId = seriesName,
                Title = seriesName,
                Year = episode.Year,
                LocalPath = episode.LocalPath
            };
        }

        return existing;
    }

    public IReadOnlyList<LibraryItemRecord> GetCatalog() => _library.GetAll();

    [GeneratedRegex(@"S(?<season>\d{1,2})\s?E(?<episode>\d{1,3})(?:\s*[-_. ]\s*(?<title>.*))?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CreateSeasonEpisodeRegex();

    [GeneratedRegex(@"(?<!\d)(19\d{2}|20\d{2})(?!\d)")]
    private static partial Regex CreateYearRegex();

    private static int? MatchYear(string name)
    {
        var match = YearRegex.Match(name);
        return match.Success ? int.Parse(match.Value) : null;
    }

    private static string CleanName(string raw)
    {
        var cleaned = YearRegex.Replace(raw, "").Trim(' ', '.', '-', '_', '(', ')');
        cleaned = cleaned.Replace('.', ' ').Replace('_', ' ');
        return cleaned.Trim();
    }
}
