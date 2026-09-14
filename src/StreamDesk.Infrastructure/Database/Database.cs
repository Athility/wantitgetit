using System;
using System.IO;
using Microsoft.Data.Sqlite;

namespace StreamDesk.Infrastructure.Database;

/// <summary>
/// Lightweight SQLite storage with forward-only migrations. The database lives in
/// the per-user app data directory so upgrades and reinstalls keep user data.
/// </summary>
public sealed class Database : IDisposable
{
    private readonly SqliteConnection _connection;

    public Database(string databasePath)
    {
        DatabasePath = databasePath;
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        _connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString());
        _connection.Open();
        using var command = _connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode=WAL; PRAGMA foreign_keys=ON;";
        command.ExecuteNonQuery();
    }

    public string DatabasePath { get; }

    internal SqliteConnection Connection => _connection;

    /// <summary>Current schema version; 0 when the database is new.</summary>
    public int SchemaVersion
    {
        get
        {
            using var command = _connection.CreateCommand();
            command.CommandText = "SELECT COALESCE((SELECT MAX(version) FROM schema_version), 0)";
            try
            {
                return Convert.ToInt32(command.ExecuteScalar());
            }
            catch (SqliteException)
            {
                return 0;
            }
        }
    }

    /// <summary>Applies all pending migrations in order. Idempotent.</summary>
    public void Migrate()
    {
        using var transaction = _connection.BeginTransaction();
        void Apply(int version, string sql)
        {
            using var command = _connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = sql;
            command.ExecuteNonQuery();
            command.CommandText = "INSERT INTO schema_version(version, applied_at_utc) VALUES ($version, $now)";
            command.Parameters.AddWithValue("$version", version);
            command.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("o"));
            command.ExecuteNonQuery();
        }

        if (SchemaVersion < 1)
        {
            Apply(1, """
                CREATE TABLE settings (
                    key TEXT PRIMARY KEY,
                    value TEXT NOT NULL
                );

                CREATE TABLE favorites (
                    item_id TEXT NOT NULL,
                    provider_id TEXT NOT NULL,
                    media_type INTEGER NOT NULL,
                    title TEXT NOT NULL,
                    poster_url TEXT,
                    year INTEGER,
                    rating REAL,
                    category TEXT,
                    added_at_utc TEXT NOT NULL,
                    PRIMARY KEY (provider_id, item_id)
                );

                CREATE TABLE watch_history (
                    item_id TEXT NOT NULL,
                    provider_id TEXT NOT NULL,
                    media_type INTEGER NOT NULL,
                    series_id TEXT NOT NULL DEFAULT '',
                    title TEXT NOT NULL,
                    subtitle TEXT,
                    poster_url TEXT,
                    season_number INTEGER,
                    episode_number INTEGER,
                    position_seconds REAL NOT NULL DEFAULT 0,
                    duration_seconds REAL,
                    completed INTEGER NOT NULL DEFAULT 0,
                    updated_at_utc TEXT NOT NULL,
                    PRIMARY KEY (provider_id, item_id)
                );

                CREATE TABLE playlists (
                    id TEXT PRIMARY KEY,
                    name TEXT NOT NULL,
                    is_remote INTEGER NOT NULL,
                    url TEXT NOT NULL DEFAULT '',
                    file_path TEXT NOT NULL DEFAULT '',
                    enabled INTEGER NOT NULL DEFAULT 1,
                    last_refreshed_utc TEXT
                );

                CREATE TABLE channels (
                    id TEXT NOT NULL,
                    playlist_id TEXT NOT NULL REFERENCES playlists(id) ON DELETE CASCADE,
                    name TEXT NOT NULL,
                    stream_url TEXT NOT NULL,
                    "group" TEXT NOT NULL DEFAULT 'Ungrouped',
                    logo_url TEXT,
                    PRIMARY KEY (playlist_id, id)
                );

                CREATE TABLE library_folders (
                    id TEXT PRIMARY KEY,
                    path TEXT NOT NULL UNIQUE,
                    enabled INTEGER NOT NULL DEFAULT 1
                );

                CREATE TABLE downloads (
                    id TEXT PRIMARY KEY,
                    item_id TEXT NOT NULL,
                    provider_id TEXT NOT NULL,
                    title TEXT NOT NULL,
                    sub_label TEXT,
                    url TEXT NOT NULL,
                    destination_path TEXT NOT NULL,
                    bytes_downloaded INTEGER NOT NULL DEFAULT 0,
                    total_bytes INTEGER,
                    status INTEGER NOT NULL DEFAULT 0,
                    error_message TEXT,
                    created_at_utc TEXT NOT NULL,
                    updated_at_utc TEXT NOT NULL
                );

                CREATE TABLE media_library (
                    item_id TEXT NOT NULL,
                    provider_id TEXT NOT NULL,
                    media_type INTEGER NOT NULL,
                    series_id TEXT NOT NULL DEFAULT '',
                    title TEXT NOT NULL,
                    subtitle TEXT,
                    year INTEGER,
                    poster_path TEXT,
                    local_path TEXT NOT NULL,
                    season_number INTEGER,
                    episode_number INTEGER,
                    file_size_bytes INTEGER,
                    discovered_at_utc TEXT NOT NULL,
                    PRIMARY KEY (provider_id, item_id)
                );

                CREATE INDEX idx_media_library_series ON media_library(provider_id, series_id, season_number, episode_number);
                CREATE INDEX idx_watch_history_updated ON watch_history(updated_at_utc DESC);
                """);
        }

        transaction.Commit();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}
