using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Data.Sqlite;
using StreamDesk.Core;

namespace StreamDesk.Infrastructure.Database;

/// <summary>SQLite-backed key/value settings store (no secrets should be stored here).</summary>
public sealed class SettingsRepository
{
    private readonly Database _database;

    public SettingsRepository(Database database)
    {
        _database = database;
    }

    public string? Get(string key)
    {
        using var command = _database.Connection.CreateCommand();
        command.CommandText = "SELECT value FROM settings WHERE key = $key";
        command.Parameters.AddWithValue("$key", key);
        var result = command.ExecuteScalar();
        return result is string value ? value : null;
    }

    public void Set(string key, string? value)
    {
        using var command = _database.Connection.CreateCommand();
        command.CommandText = """
            INSERT INTO settings(key, value) VALUES ($key, $value)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value
            """;
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", (object?)value ?? DBNull.Value);
        command.ExecuteNonQuery();
    }

    public IReadOnlyDictionary<string, string> GetAll()
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        using var command = _database.Connection.CreateCommand();
        command.CommandText = "SELECT key, value FROM settings";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            result[reader.GetString(0)] = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
        }

        return result;
    }
}

/// <summary>SQLite-backed favorites store.</summary>
public sealed class FavoritesRepository
{
    private readonly Database _database;

    public FavoritesRepository(Database database)
    {
        _database = database;
    }

    public IReadOnlyList<FavoriteItem> GetAll()
    {
        var favorites = new List<FavoriteItem>();
        using var command = _database.Connection.CreateCommand();
        command.CommandText = "SELECT item_id, provider_id, media_type, title, poster_url, year, rating, category, added_at_utc FROM favorites ORDER BY added_at_utc DESC";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            favorites.Add(new FavoriteItem
            {
                ItemId = reader.GetString(0),
                ProviderId = reader.GetString(1),
                MediaType = (MediaType)reader.GetInt32(2),
                Title = reader.GetString(3),
                PosterUrl = reader.IsDBNull(4) ? null : reader.GetString(4),
                Year = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                Rating = reader.IsDBNull(6) ? null : reader.GetDouble(6),
                Category = reader.IsDBNull(7) ? null : reader.GetString(7),
                AddedAtUtc = RepositoryExtensions.ParseUtc(reader.GetString(8))
            });
        }

        return favorites;
    }

    public bool IsFavorite(string providerId, string itemId)
    {
        using var command = _database.Connection.CreateCommand();
        command.CommandText = "SELECT COUNT(1) FROM favorites WHERE provider_id = $p AND item_id = $i";
        command.Parameters.AddWithValue("$p", providerId);
        command.Parameters.AddWithValue("$i", itemId);
        return Convert.ToInt64(command.ExecuteScalar()) > 0;
    }

    public void Add(FavoriteItem favorite)
    {
        using var command = _database.Connection.CreateCommand();
        command.CommandText = """
            INSERT OR REPLACE INTO favorites(item_id, provider_id, media_type, title, poster_url, year, rating, category, added_at_utc)
            VALUES ($i, $p, $t, $ti, $po, $y, $r, $c, $a)
            """;
        command.Parameters.AddWithValue("$i", favorite.ItemId);
        command.Parameters.AddWithValue("$p", favorite.ProviderId);
        command.Parameters.AddWithValue("$t", (int)favorite.MediaType);
        command.Parameters.AddWithValue("$ti", favorite.Title);
        command.Parameters.AddWithValue("$po", (object?)favorite.PosterUrl ?? DBNull.Value);
        command.Parameters.AddWithValue("$y", (object?)favorite.Year ?? DBNull.Value);
        command.Parameters.AddWithValue("$r", (object?)favorite.Rating ?? DBNull.Value);
        command.Parameters.AddWithValue("$c", (object?)favorite.Category ?? DBNull.Value);
        command.Parameters.AddWithValue("$a", favorite.AddedAtUtc.ToString("o"));
        command.ExecuteNonQuery();
    }

    public void Remove(string providerId, string itemId)
    {
        using var command = _database.Connection.CreateCommand();
        command.CommandText = "DELETE FROM favorites WHERE provider_id = $p AND item_id = $i";
        command.Parameters.AddWithValue("$p", providerId);
        command.Parameters.AddWithValue("$i", itemId);
        command.ExecuteNonQuery();
    }
}

/// <summary>SQLite-backed watch history / playback position store.</summary>
public sealed class WatchHistoryRepository
{
    private readonly Database _database;

    public WatchHistoryRepository(Database database)
    {
        _database = database;
    }

    public IReadOnlyList<WatchHistoryEntry> GetRecent(int limit = 30)
    {
        var entries = new List<WatchHistoryEntry>();
        using var command = _database.Connection.CreateCommand();
        command.CommandText = """
            SELECT item_id, provider_id, media_type, series_id, title, subtitle, poster_url, season_number, episode_number,
                   position_seconds, duration_seconds, completed, updated_at_utc
            FROM watch_history ORDER BY updated_at_utc DESC LIMIT $limit
            """;
        command.Parameters.AddWithValue("$limit", limit);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            entries.Add(ReadEntry(reader));
        }

        return entries;
    }

    public WatchHistoryEntry? Get(string providerId, string itemId)
    {
        using var command = _database.Connection.CreateCommand();
        command.CommandText = """
            SELECT item_id, provider_id, media_type, series_id, title, subtitle, poster_url, season_number, episode_number,
                   position_seconds, duration_seconds, completed, updated_at_utc
            FROM watch_history WHERE provider_id = $p AND item_id = $i
            """;
        command.Parameters.AddWithValue("$p", providerId);
        command.Parameters.AddWithValue("$i", itemId);
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadEntry(reader) : null;
    }

    public void Upsert(WatchHistoryEntry entry)
    {
        using var command = _database.Connection.CreateCommand();
        command.CommandText = """
            INSERT INTO watch_history(item_id, provider_id, media_type, series_id, title, subtitle, poster_url, season_number,
                                      episode_number, position_seconds, duration_seconds, completed, updated_at_utc)
            VALUES ($i, $p, $t, $s, $ti, $su, $po, $sn, $en, $pos, $dur, $c, $u)
            ON CONFLICT(provider_id, item_id) DO UPDATE SET
                media_type = excluded.media_type, series_id = excluded.series_id, title = excluded.title,
                subtitle = excluded.subtitle, poster_url = excluded.poster_url, season_number = excluded.season_number,
                episode_number = excluded.episode_number, position_seconds = excluded.position_seconds,
                duration_seconds = excluded.duration_seconds, completed = excluded.completed,
                updated_at_utc = excluded.updated_at_utc
            """;
        command.Parameters.AddWithValue("$i", entry.ItemId);
        command.Parameters.AddWithValue("$p", entry.ProviderId);
        command.Parameters.AddWithValue("$t", (int)entry.MediaType);
        command.Parameters.AddWithValue("$s", entry.SeriesId);
        command.Parameters.AddWithValue("$ti", entry.Title);
        command.Parameters.AddWithValue("$su", (object?)entry.Subtitle ?? DBNull.Value);
        command.Parameters.AddWithValue("$po", (object?)entry.PosterUrl ?? DBNull.Value);
        command.Parameters.AddWithValue("$sn", (object?)entry.SeasonNumber ?? DBNull.Value);
        command.Parameters.AddWithValue("$en", (object?)entry.EpisodeNumber ?? DBNull.Value);
        command.Parameters.AddWithValue("$pos", entry.PositionSeconds);
        command.Parameters.AddWithValue("$dur", (object?)entry.DurationSeconds ?? DBNull.Value);
        command.Parameters.AddWithValue("$c", entry.Completed ? 1 : 0);
        command.Parameters.AddWithValue("$u", entry.UpdatedAtUtc.ToString("o"));
        command.ExecuteNonQuery();
    }

    public void Remove(string providerId, string itemId)
    {
        using var command = _database.Connection.CreateCommand();
        command.CommandText = "DELETE FROM watch_history WHERE provider_id = $p AND item_id = $i";
        command.Parameters.AddWithValue("$p", providerId);
        command.Parameters.AddWithValue("$i", itemId);
        command.ExecuteNonQuery();
    }

    private static WatchHistoryEntry ReadEntry(SqliteDataReader reader)
    {
        return new WatchHistoryEntry
        {
            ItemId = reader.GetString(0),
            ProviderId = reader.GetString(1),
            MediaType = (MediaType)reader.GetInt32(2),
            SeriesId = reader.GetString(3),
            Title = reader.GetString(4),
            Subtitle = reader.IsDBNull(5) ? null : reader.GetString(5),
            PosterUrl = reader.IsDBNull(6) ? null : reader.GetString(6),
            SeasonNumber = reader.IsDBNull(7) ? null : reader.GetInt32(7),
            EpisodeNumber = reader.IsDBNull(8) ? null : reader.GetInt32(8),
            PositionSeconds = reader.GetDouble(9),
            DurationSeconds = reader.IsDBNull(10) ? null : reader.GetDouble(10),
            Completed = reader.GetInt32(11) != 0,
            UpdatedAtUtc = RepositoryExtensions.ParseUtc(reader.GetString(12))
        };
    }
}

/// <summary>SQLite-backed download task store (survives restarts for resume).</summary>
public sealed class DownloadsRepository
{
    private readonly Database _database;

    public DownloadsRepository(Database database)
    {
        _database = database;
    }

    public IReadOnlyList<DownloadTask> GetAll()
    {
        var tasks = new List<DownloadTask>();
        using var command = _database.Connection.CreateCommand();
        command.CommandText = "SELECT id, item_id, provider_id, title, sub_label, url, destination_path, bytes_downloaded, total_bytes, status, error_message, created_at_utc, updated_at_utc FROM downloads ORDER BY created_at_utc";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            tasks.Add(ReadTask(reader));
        }

        return tasks;
    }

    public DownloadTask? Get(string id)
    {
        using var command = _database.Connection.CreateCommand();
        command.CommandText = "SELECT id, item_id, provider_id, title, sub_label, url, destination_path, bytes_downloaded, total_bytes, status, error_message, created_at_utc, updated_at_utc FROM downloads WHERE id = $id";
        command.Parameters.AddWithValue("$id", id);
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadTask(reader) : null;
    }

    public void Upsert(DownloadTask task)
    {
        using var command = _database.Connection.CreateCommand();
        command.CommandText = """
            INSERT INTO downloads(id, item_id, provider_id, title, sub_label, url, destination_path, bytes_downloaded, total_bytes, status, error_message, created_at_utc, updated_at_utc)
            VALUES ($id, $i, $p, $t, $s, $u, $d, $b, $tb, $st, $e, $c, $up)
            ON CONFLICT(id) DO UPDATE SET
                title = excluded.title, sub_label = excluded.sub_label, bytes_downloaded = excluded.bytes_downloaded,
                total_bytes = excluded.total_bytes, status = excluded.status, error_message = excluded.error_message,
                updated_at_utc = excluded.updated_at_utc
            """;
        command.Parameters.AddWithValue("$id", task.Id);
        command.Parameters.AddWithValue("$i", task.ItemId);
        command.Parameters.AddWithValue("$p", task.ProviderId);
        command.Parameters.AddWithValue("$t", task.Title);
        command.Parameters.AddWithValue("$s", (object?)task.SubLabel ?? DBNull.Value);
        command.Parameters.AddWithValue("$u", task.Url);
        command.Parameters.AddWithValue("$d", task.DestinationPath);
        command.Parameters.AddWithValue("$b", task.BytesDownloaded);
        command.Parameters.AddWithValue("$tb", (object?)task.TotalBytes ?? DBNull.Value);
        command.Parameters.AddWithValue("$st", (int)task.Status);
        command.Parameters.AddWithValue("$e", (object?)task.ErrorMessage ?? DBNull.Value);
        command.Parameters.AddWithValue("$c", task.CreatedAtUtc.ToString("o"));
        command.Parameters.AddWithValue("$up", task.UpdatedAtUtc.ToString("o"));
        command.ExecuteNonQuery();
    }

    public void Remove(string id)
    {
        using var command = _database.Connection.CreateCommand();
        command.CommandText = "DELETE FROM downloads WHERE id = $id";
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    private static DownloadTask ReadTask(SqliteDataReader reader)
    {
        return new DownloadTask
        {
            Id = reader.GetString(0),
            ItemId = reader.GetString(1),
            ProviderId = reader.GetString(2),
            Title = reader.GetString(3),
            SubLabel = reader.IsDBNull(4) ? null : reader.GetString(4),
            Url = reader.GetString(5),
            DestinationPath = reader.GetString(6),
            BytesDownloaded = reader.GetInt64(7),
            TotalBytes = reader.IsDBNull(8) ? null : reader.GetInt64(8),
            Status = (DownloadStatus)reader.GetInt32(9),
            ErrorMessage = reader.IsDBNull(10) ? null : reader.GetString(10),
            CreatedAtUtc = RepositoryExtensions.ParseUtc(reader.GetString(11)),
            UpdatedAtUtc = RepositoryExtensions.ParseUtc(reader.GetString(12))
        };
    }
}

/// <summary>SQLite-backed M3U playlist store.</summary>
public sealed class PlaylistsRepository
{
    private readonly Database _database;

    public PlaylistsRepository(Database database)
    {
        _database = database;
    }

    public IReadOnlyList<PlaylistSource> GetAll()
    {
        var playlists = new List<PlaylistSource>();
        using var command = _database.Connection.CreateCommand();
        command.CommandText = "SELECT id, name, is_remote, url, file_path, enabled, last_refreshed_utc FROM playlists ORDER BY name";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            playlists.Add(new PlaylistSource
            {
                Id = reader.GetString(0),
                Name = reader.GetString(1),
                IsRemote = reader.GetInt32(2) != 0,
                Url = reader.GetString(3),
                FilePath = reader.GetString(4),
                Enabled = reader.GetInt32(5) != 0,
                LastRefreshedUtc = reader.IsDBNull(6) ? null : RepositoryExtensions.ParseUtc(reader.GetString(6))
            });
        }

        return playlists;
    }

    public void Upsert(PlaylistSource playlist)
    {
        using var command = _database.Connection.CreateCommand();
        command.CommandText = """
            INSERT INTO playlists(id, name, is_remote, url, file_path, enabled, last_refreshed_utc)
            VALUES ($id, $n, $r, $u, $f, $e, $l)
            ON CONFLICT(id) DO UPDATE SET
                name = excluded.name, is_remote = excluded.is_remote, url = excluded.url, file_path = excluded.file_path,
                enabled = excluded.enabled, last_refreshed_utc = excluded.last_refreshed_utc
            """;
        command.Parameters.AddWithValue("$id", playlist.Id);
        command.Parameters.AddWithValue("$n", playlist.Name);
        command.Parameters.AddWithValue("$r", playlist.IsRemote ? 1 : 0);
        command.Parameters.AddWithValue("$u", playlist.Url);
        command.Parameters.AddWithValue("$f", playlist.FilePath);
        command.Parameters.AddWithValue("$e", playlist.Enabled ? 1 : 0);
        command.Parameters.AddWithValue("$l", (object?)playlist.LastRefreshedUtc?.ToString("o") ?? DBNull.Value);
        command.ExecuteNonQuery();
    }

    public void Remove(string id)
    {
        using var command = _database.Connection.CreateCommand();
        command.CommandText = "DELETE FROM playlists WHERE id = $id";
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }
}

/// <summary>SQLite-backed Live TV channel store (cache of parsed playlists).</summary>
public sealed class ChannelsRepository
{
    private readonly Database _database;

    public ChannelsRepository(Database database)
    {
        _database = database;
    }

    public IReadOnlyList<ChannelInfo> GetByPlaylist(string playlistId)
    {
        var channels = new List<ChannelInfo>();
        using var command = _database.Connection.CreateCommand();
        command.CommandText = "SELECT id, name, stream_url, \"group\", logo_url FROM channels WHERE playlist_id = $p ORDER BY name COLLATE NOCASE";
        command.Parameters.AddWithValue("$p", playlistId);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            channels.Add(new ChannelInfo
            {
                Id = reader.GetString(0),
                Name = reader.GetString(1),
                StreamUrl = reader.GetString(2),
                Group = reader.GetString(3),
                LogoUrl = reader.IsDBNull(4) ? null : reader.GetString(4)
            });
        }

        return channels;
    }

    public void ReplaceForPlaylist(string playlistId, IEnumerable<ChannelInfo> channels)
    {
        using var transaction = _database.Connection.BeginTransaction();
        using (var delete = _database.Connection.CreateCommand())
        {
            delete.Transaction = transaction;
            delete.CommandText = "DELETE FROM channels WHERE playlist_id = $p";
            delete.Parameters.AddWithValue("$p", playlistId);
            delete.ExecuteNonQuery();
        }

        using (var insert = _database.Connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = """
                INSERT OR REPLACE INTO channels(id, playlist_id, name, stream_url, "group", logo_url)
                VALUES ($id, $p, $n, $u, $g, $l)
                """;
            var id = insert.Parameters.Add("$id", SqliteType.Text);
            var playlist = insert.Parameters.Add("$p", SqliteType.Text);
            var name = insert.Parameters.Add("$n", SqliteType.Text);
            var url = insert.Parameters.Add("$u", SqliteType.Text);
            var group = insert.Parameters.Add("$g", SqliteType.Text);
            var logo = insert.Parameters.Add("$l", SqliteType.Text);
            foreach (var channel in channels)
            {
                id.Value = channel.Id;
                playlist.Value = playlistId;
                name.Value = channel.Name;
                url.Value = channel.StreamUrl;
                group.Value = channel.Group;
                logo.Value = (object?)channel.LogoUrl ?? DBNull.Value;
                insert.ExecuteNonQuery();
            }
        }

        transaction.Commit();
    }
}

/// <summary>SQLite-backed local library catalog.</summary>
public sealed class MediaLibraryRepository
{
    private readonly Database _database;

    public MediaLibraryRepository(Database database)
    {
        _database = database;
    }

    public IReadOnlyList<LibraryItemRecord> GetAll()
    {
        var items = new List<LibraryItemRecord>();
        using var command = _database.Connection.CreateCommand();
        command.CommandText = "SELECT item_id, provider_id, media_type, series_id, title, subtitle, year, poster_path, local_path, season_number, episode_number, file_size_bytes, discovered_at_utc FROM media_library";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            items.Add(new LibraryItemRecord
            {
                ItemId = reader.GetString(0),
                ProviderId = reader.GetString(1),
                MediaType = (MediaType)reader.GetInt32(2),
                SeriesId = reader.GetString(3),
                Title = reader.GetString(4),
                Subtitle = reader.IsDBNull(5) ? null : reader.GetString(5),
                Year = reader.IsDBNull(6) ? null : reader.GetInt32(6),
                PosterPath = reader.IsDBNull(7) ? null : reader.GetString(7),
                LocalPath = reader.GetString(8),
                SeasonNumber = reader.IsDBNull(9) ? null : reader.GetInt32(9),
                EpisodeNumber = reader.IsDBNull(10) ? null : reader.GetInt32(10),
                FileSizeBytes = reader.IsDBNull(11) ? null : reader.GetInt64(11),
                DiscoveredAtUtc = RepositoryExtensions.ParseUtc(reader.GetString(12))
            });
        }

        return items;
    }

    public void ReplaceForFolder(string folderId, IEnumerable<LibraryItemRecord> items)
    {
        using var transaction = _database.Connection.BeginTransaction();
        using (var delete = _database.Connection.CreateCommand())
        {
            delete.Transaction = transaction;
            delete.CommandText = "DELETE FROM media_library WHERE provider_id = $p";
            delete.Parameters.AddWithValue("$p", folderId);
            delete.ExecuteNonQuery();
        }

        using (var insert = _database.Connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = """
                INSERT OR REPLACE INTO media_library(item_id, provider_id, media_type, series_id, title, subtitle, year, poster_path,
                                                    local_path, season_number, episode_number, file_size_bytes, discovered_at_utc)
                VALUES ($i, $p, $t, $s, $ti, $su, $y, $po, $l, $sn, $en, $fs, $d)
                """;
            var itemId = insert.Parameters.Add("$i", SqliteType.Text);
            var provider = insert.Parameters.Add("$p", SqliteType.Text);
            var type = insert.Parameters.Add("$t", SqliteType.Integer);
            var series = insert.Parameters.Add("$s", SqliteType.Text);
            var title = insert.Parameters.Add("$ti", SqliteType.Text);
            var subtitle = insert.Parameters.Add("$su", SqliteType.Text);
            var year = insert.Parameters.Add("$y", SqliteType.Integer);
            var poster = insert.Parameters.Add("$po", SqliteType.Text);
            var local = insert.Parameters.Add("$l", SqliteType.Text);
            var season = insert.Parameters.Add("$sn", SqliteType.Integer);
            var episode = insert.Parameters.Add("$en", SqliteType.Integer);
            var size = insert.Parameters.Add("$fs", SqliteType.Integer);
            var discovered = insert.Parameters.Add("$d", SqliteType.Text);
            foreach (var item in items)
            {
                itemId.Value = item.ItemId;
                provider.Value = item.ProviderId;
                type.Value = (int)item.MediaType;
                series.Value = item.SeriesId;
                title.Value = item.Title;
                subtitle.Value = (object?)item.Subtitle ?? DBNull.Value;
                year.Value = (object?)item.Year ?? DBNull.Value;
                poster.Value = (object?)item.PosterPath ?? DBNull.Value;
                local.Value = item.LocalPath;
                season.Value = (object?)item.SeasonNumber ?? DBNull.Value;
                episode.Value = (object?)item.EpisodeNumber ?? DBNull.Value;
                size.Value = (object?)item.FileSizeBytes ?? DBNull.Value;
                discovered.Value = item.DiscoveredAtUtc.ToString("o");
                insert.ExecuteNonQuery();
            }
        }

        transaction.Commit();
    }
}

/// <summary>Flat record for a scanned local media file.</summary>
public sealed class LibraryItemRecord
{
    public string ItemId { get; set; } = string.Empty;

    public string ProviderId { get; set; } = string.Empty;

    public MediaType MediaType { get; set; }

    public string SeriesId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string? Subtitle { get; set; }

    public int? Year { get; set; }

    /// <summary>Poster path if a matching image file sits next to the media.</summary>
    public string? PosterPath { get; set; }

    public string LocalPath { get; set; } = string.Empty;

    public int? SeasonNumber { get; set; }

    public int? EpisodeNumber { get; set; }

    public long? FileSizeBytes { get; set; }

    public DateTime DiscoveredAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>SQLite-backed local library folder list.</summary>
public sealed class LibraryFoldersRepository
{
    private readonly Database _database;

    public LibraryFoldersRepository(Database database)
    {
        _database = database;
    }

    public IReadOnlyList<LibraryFolder> GetAll()
    {
        var folders = new List<LibraryFolder>();
        using var command = _database.Connection.CreateCommand();
        command.CommandText = "SELECT id, path, enabled FROM library_folders ORDER BY path";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            folders.Add(new LibraryFolder
            {
                Id = reader.GetString(0),
                Path = reader.GetString(1),
                Enabled = reader.GetInt32(2) != 0
            });
        }

        return folders;
    }

    public void Upsert(LibraryFolder folder)
    {
        using var command = _database.Connection.CreateCommand();
        command.CommandText = """
            INSERT INTO library_folders(id, path, enabled) VALUES ($id, $p, $e)
            ON CONFLICT(id) DO UPDATE SET path = excluded.path, enabled = excluded.enabled
            """;
        command.Parameters.AddWithValue("$id", folder.Id);
        command.Parameters.AddWithValue("$p", folder.Path);
        command.Parameters.AddWithValue("$e", folder.Enabled ? 1 : 0);
        command.ExecuteNonQuery();
    }

    public void Remove(string id)
    {
        using var command = _database.Connection.CreateCommand();
        command.CommandText = "DELETE FROM library_folders WHERE id = $id";
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }
}

internal static class RepositoryExtensions
{
    internal static DateTime ParseUtc(string value) =>
        DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed
            : DateTime.UtcNow;
}
