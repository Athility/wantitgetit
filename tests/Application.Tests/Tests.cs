using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using StreamDesk.Application.Managers;
using StreamDesk.Application.Services;
using StreamDesk.Core;
using StreamDesk.Infrastructure.Database;
using StreamDesk.Infrastructure.Storage;
using Xunit;

namespace Application.Tests;

/// <summary>Fake provider for exercising ProviderManager without network.</summary>
internal sealed class FakeProvider : IMediaProvider, ISearchProvider, IPlaybackProvider
{
    public FakeProvider(string id, bool configured, ProviderCapabilities capabilities)
    {
        Descriptor = new ProviderDescriptor(id, id, "fake")
        {
            Capabilities = capabilities,
            IsConfigured = configured,
            IsEnabled = true
        };
    }

    public ProviderDescriptor Descriptor { get; set; }

    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<MediaPage> GetSectionAsync(string sectionName, int page, CancellationToken cancellationToken = default) =>
        Task.FromResult(new MediaPage(new[] { new MediaSummary("fake:movie:1", Descriptor.Id, MediaType.Movie, "Fake Movie") }, page, false));

    public Task<MediaDetails?> GetDetailsAsync(string itemId, CancellationToken cancellationToken = default) =>
        Task.FromResult<MediaDetails?>(new MediaDetails(itemId, Descriptor.Id, MediaType.Movie, "Fake Movie"));

    public Task<IReadOnlyList<SeasonInfo>?> GetSeasonsAsync(string itemId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<SeasonInfo>?>(null);

    public Task<IReadOnlyList<MediaSummary>> SearchAsync(SearchRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<MediaSummary>>(new[] { new MediaSummary("fake:movie:1", Descriptor.Id, MediaType.Movie, "Fake " + request.Query) });

    public Task<PlaybackSource?> ResolvePlaybackAsync(string itemId, CancellationToken cancellationToken = default) =>
        Task.FromResult<PlaybackSource?>(new PlaybackSource(PlaybackSourceKind.LocalFile, @"C:\fake\movie.mkv"));
}

public class ProviderManagerTests
{
    private static ProviderCapabilities CatalogCaps =>
        ProviderCapabilities.Movies | ProviderCapabilities.Search | ProviderCapabilities.Playback | ProviderCapabilities.HomeSections;

    [Fact]
    public void Register_AddsProvider_AndPreventsDuplicates()
    {
        var manager = new ProviderManager();
        manager.Register(new FakeProvider("a", true, CatalogCaps));
        manager.Register(new FakeProvider("a", true, CatalogCaps));

        Assert.Single(manager.GetDescriptors());
    }

    [Fact]
    public async Task UnconfiguredProviders_AreExcludedFromUsableSet()
    {
        var manager = new ProviderManager();
        manager.Register(new FakeProvider("configured", true, CatalogCaps));
        manager.Register(new FakeProvider("unconfigured", false, CatalogCaps));
        await manager.InitializeAllAsync();

        Assert.Single(manager.GetEnabledProviders());
        Assert.False(manager.IsUsable("unconfigured"));
        Assert.True(manager.IsUsable("configured"));
    }

    [Fact]
    public async Task DisableSwitch_ExcludesProvider()
    {
        var manager = new ProviderManager(id => id != "disabled");
        manager.Register(new FakeProvider("enabled", true, CatalogCaps));
        manager.Register(new FakeProvider("disabled", true, CatalogCaps));
        await manager.InitializeAllAsync();

        Assert.Single(manager.GetEnabledProviders());
    }

    [Fact]
    public async Task Search_MergesAcrossProviders()
    {
        var manager = new ProviderManager();
        manager.Register(new FakeProvider("a", true, CatalogCaps));
        manager.Register(new FakeProvider("b", true, CatalogCaps));
        await manager.InitializeAllAsync();

        var results = await manager.SearchAsync(new SearchRequest("test"));
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task Search_UnconfiguredProviderContributesNothing()
    {
        var manager = new ProviderManager();
        manager.Register(new FakeProvider("a", true, CatalogCaps));
        manager.Register(new FakeProvider("b", false, CatalogCaps));
        await manager.InitializeAllAsync();

        var results = await manager.SearchAsync(new SearchRequest("test"));
        Assert.Single(results);
    }

    [Fact]
    public async Task FailingProvider_DoesNotBreakSearch()
    {
        var manager = new ProviderManager();
        manager.Register(new ThrowingProvider());
        manager.Register(new FakeProvider("ok", true, CatalogCaps));
        await manager.InitializeAllAsync();

        var results = await manager.SearchAsync(new SearchRequest("test"));
        Assert.Single(results);
    }

    [Fact]
    public async Task HasCapability_ReflectsDescriptor()
    {
        var manager = new ProviderManager();
        manager.Register(new FakeProvider("a", true, ProviderCapabilities.Search));
        await manager.InitializeAllAsync();

        Assert.True(manager.HasCapability("a", ProviderCapabilities.Search));
        Assert.False(manager.HasCapability("a", ProviderCapabilities.Downloads));
    }

    [Fact]
    public async Task ResolvePlayback_RoutesToOwningProvider()
    {
        var manager = new ProviderManager();
        manager.Register(new FakeProvider("a", true, CatalogCaps));
        await manager.InitializeAllAsync();

        var source = await manager.ResolvePlaybackAsync("fake:movie:1", "a");
        Assert.NotNull(source);
        Assert.Equal(PlaybackSourceKind.LocalFile, source!.Kind);
    }

    [Fact]
    public async Task ResolvePlayback_UnknownProvider_ReturnsNull()
    {
        var manager = new ProviderManager();
        await manager.InitializeAllAsync();

        var source = await manager.ResolvePlaybackAsync("fake:movie:1", "ghost");
        Assert.Null(source);
    }

    private sealed class ThrowingProvider : IMediaProvider, ISearchProvider
    {
        public ProviderDescriptor Descriptor { get; } = new("boom", "boom", "throws")
        {
            Capabilities = CatalogCaps,
            IsConfigured = true,
            IsEnabled = true
        };

        public Task InitializeAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException("startup failure");

        public Task<MediaPage> GetSectionAsync(string sectionName, int page, CancellationToken cancellationToken = default) =>
            throw new HttpRequestException("network down");

        public Task<MediaDetails?> GetDetailsAsync(string itemId, CancellationToken cancellationToken = default) => throw new HttpRequestException("network down");

        public Task<IReadOnlyList<SeasonInfo>?> GetSeasonsAsync(string itemId, CancellationToken cancellationToken = default) => throw new HttpRequestException("network down");

        public Task<IReadOnlyList<MediaSummary>> SearchAsync(SearchRequest request, CancellationToken cancellationToken = default) => throw new HttpRequestException("network down");
    }
}

/// <summary>In-memory settings store for tests (no disk writes).</summary>
public sealed class InMemorySettingsStore : IAppSettingsStore
{
    private AppSettings _current = new();

    public int SaveCount { get; private set; }

    public AppSettings Load() => _current;

    public void Save(AppSettings settings)
    {
        _current = settings;
        SaveCount++;
    }
}

public class FavoritesAndHistoryTests : IDisposable
{
    private readonly Database _database;

    public FavoritesAndHistoryTests()
    {
        _database = new Database(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sd-test-" + Guid.NewGuid().ToString("N") + ".db"));
        _database.Migrate();
    }

    public void Dispose()
    {
        _database.Dispose();
        TryDelete(_database.DatabasePath);
    }

    [Fact]
    public void Favorites_Toggle_Persists()
    {
        var service = new FavoritesService(new FavoritesRepository(_database));
        var summary = new MediaSummary("tmdb:movie:603", "tmdb", MediaType.Movie, "The Matrix") { Year = 1999, Rating = 8.4 };

        service.Toggle(summary);
        Assert.True(service.IsFavorite("tmdb", "tmdb:movie:603"));

        service.Toggle(summary);
        Assert.False(service.IsFavorite("tmdb", "tmdb:movie:603"));
    }

    [Fact]
    public void Favorites_PersistAcrossRepositoryInstances()
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sd-test-" + Guid.NewGuid().ToString("N") + ".db");
        using (var db = new Database(path))
        {
            db.Migrate();
            var service = new FavoritesService(new FavoritesRepository(db));
            service.Toggle(new MediaSummary("local:movie:1", "local", MediaType.Movie, "Local Film"));
        }

        using (var db2 = new Database(path))
        {
            var favorites = new FavoritesRepository(db2).GetAll();
            Assert.Single(favorites);
            Assert.Equal("Local Film", favorites[0].Title);
        }

        TryDelete(path);
    }

    [Fact]
    public void WatchHistory_RecordAndContinueWatching()
    {
        var service = new WatchHistoryService(new WatchHistoryRepository(_database));

        service.RecordProgress(new WatchHistoryEntry
        {
            ItemId = "ep:1",
            ProviderId = "local",
            MediaType = MediaType.Episode,
            Title = "Show",
            Subtitle = "S01 E04 - Pilot",
            PositionSeconds = 300,
            DurationSeconds = 1800
        });

        var continueWatching = service.GetContinueWatching();
        Assert.Single(continueWatching);
        Assert.Equal(300, continueWatching[0].PositionSeconds);
        Assert.Equal(16.7, continueWatching[0].PercentComplete, 0);
    }

    [Fact]
    public void Settings_ResetToDefaults_RestoresFreshValues()
    {
        var settings = new SettingsService(new InMemorySettingsStore());
        settings.Current.PreferredPlayer = PlayerKind.Mpv;
        settings.Save();

        settings.ResetToDefaults();
        Assert.Equal(PlayerKind.AutoDetect, settings.Current.PreferredPlayer);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // best effort cleanup
        }
    }
}

public class PlaybackServiceTests
{
    [Fact]
    public void DetectPlayers_ReturnsDictionaryWithoutThrowing()
    {
        var settings = new SettingsService(new InMemorySettingsStore());
        var service = new PlaybackService(settings);

        var detected = service.DetectPlayers();

        Assert.Contains(PlayerKind.Vlc, (IDictionary<PlayerKind, string?>)detected);
        Assert.Contains(PlayerKind.Mpv, (IDictionary<PlayerKind, string?>)detected);
    }

    [Fact]
    public async Task PlayAsync_EmptySource_ReturnsFriendlyError()
    {
        var settings = new SettingsService(new InMemorySettingsStore());
        var service = new PlaybackService(settings);

        var error = await service.PlayAsync(new PlaybackSource(PlaybackSourceKind.LocalFile, ""));

        Assert.NotNull(error);
        Assert.Contains("Unable to play", error);
    }

    [Fact]
    public async Task PlayAsync_MissingFile_ReturnsFriendlyError()
    {
        var settings = new SettingsService(new InMemorySettingsStore());
        var service = new PlaybackService(settings);

        var error = await service.PlayAsync(new PlaybackSource(PlaybackSourceKind.LocalFile, @"C:\definitely\not\here.mkv"));

        Assert.NotNull(error);
        Assert.Contains("no longer exists", error);
    }

    [Fact]
    public async Task PlayAsync_MissingConfiguredPlayer_ReturnsFriendlyError()
    {
        var settings = new SettingsService(new InMemorySettingsStore());
        settings.Current.PreferredPlayer = PlayerKind.Vlc;
        settings.Current.VlcPath = @"C:\no\vlc\here.exe";
        var service = new PlaybackService(settings);

        // Use an existing file so the player check is the failure point.
        var mediaPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sd-play-" + Guid.NewGuid().ToString("N") + ".mkv");
        await File.WriteAllTextAsync(mediaPath, "video");
        try
        {
            var error = await service.PlayAsync(new PlaybackSource(PlaybackSourceKind.LocalFile, mediaPath));

            Assert.NotNull(error);
            Assert.Contains("VLC was not found", error);
        }
        finally
        {
            File.Delete(mediaPath);
        }
    }
}

public class SubtitleServiceTests
{
    [Fact]
    public void FindLocalSubtitleTracks_MatchesSidecarFiles()
    {
        var directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sd-subs-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var media = System.IO.Path.Combine(directory, "Movie.2020.mkv");
            File.WriteAllText(media, "video");
            File.WriteAllText(System.IO.Path.Combine(directory, "Movie.2020.en.srt"), "1\n00:00:01,000 --> 00:00:02,000\nHi\n");
            File.WriteAllText(System.IO.Path.Combine(directory, "Movie.2020.hi.vtt"), "WEBVTT\n");
            File.WriteAllText(System.IO.Path.Combine(directory, "Unrelated.ass"), "unrelated");

            var service = new SubtitleService();
            var tracks = service.FindLocalSubtitleTracks(media, "en");

            Assert.Equal(2, tracks.Count);
            Assert.Contains(tracks, t => t.Language == "en" && t.Location.EndsWith(".srt"));
            Assert.Contains(tracks, t => t.Language == "hi" && t.Location.EndsWith(".vtt"));
            Assert.Contains(tracks, t => t.IsDefault);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void FindLocalSubtitleTracks_MissingFile_ReturnsEmpty()
    {
        var service = new SubtitleService();
        Assert.Empty(service.FindLocalSubtitleTracks(@"C:\no\file.mkv", "auto"));
    }
}

/// <summary>Local HTTP server exposing a deterministic 100 KB payload for download tests.</summary>
internal sealed class LocalHttpServer : IDisposable
{
    private readonly HttpListener _listener;
    private const int PayloadBytes = 100_000;

    public LocalHttpServer()
    {
        Port = FindFreePort();
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
        _listener.Start();
        _ = Task.Run(AcceptLoopAsync);
    }

    public string Url => $"http://127.0.0.1:{Port}/";

    public int Port { get; }

    public byte[] Payload { get; } = CreatePayload(PayloadBytes);

    private static int FindFreePort()
    {
        var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    private static byte[] CreatePayload(int length)
    {
        var payload = new byte[length];
        new Random(42).NextBytes(payload);
        return payload;
    }

    private async Task AcceptLoopAsync()
    {
        while (_listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync();
            }
            catch (Exception)
            {
                return; // listener stopped
            }

            _ = Task.Run(() => HandleAsync(context));
        }
    }

    private async Task HandleAsync(HttpListenerContext context)
    {
        try
        {
            var range = context.Request.Headers["Range"];
            long start = 0;
            if (range is not null && range.StartsWith("bytes=", StringComparison.OrdinalIgnoreCase))
            {
                var first = range[6..].Split('-')[0];
                if (long.TryParse(first, out var parsed))
                {
                    start = parsed;
                }
            }

            var total = Payload.Length;
            context.Response.StatusCode = start > 0 ? (int)HttpStatusCode.PartialContent : (int)HttpStatusCode.OK;
            context.Response.ContentType = "application/octet-stream";
            context.Response.ContentLength64 = total - start;

            if (start > 0)
            {
                context.Response.Headers["Content-Range"] = $"bytes {start}-{total - 1}/{total}";
            }

            await using var output = context.Response.OutputStream;
            await output.WriteAsync(Payload.AsMemory((int)start, (int)(total - start)));
            await output.FlushAsync();
            context.Response.Close();
        }
        catch (Exception)
        {
            try { context.Response.Abort(); } catch { /* ignore */ }
        }
    }

    public void Dispose()
    {
        _listener.Stop();
        _listener.Close();
    }
}

public class DownloadManagerTests : IDisposable
{
    private readonly LocalHttpServer _server = new();
    private readonly string _downloadDirectory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sd-down-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        _server.Dispose();
        try
        {
            Directory.Delete(_downloadDirectory, recursive: true);
        }
        catch (DirectoryNotFoundException)
        {
        }
    }

    private DownloadManagerOptions Options() => new()
    {
        DownloadDirectory = _downloadDirectory,
        MaxConcurrentDownloads = 2,
        AllowResume = true
    };

    private static Database NewDatabase(out string path)
    {
        path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sd-test-" + Guid.NewGuid().ToString("N") + ".db");
        var database = new Database(path);
        database.Migrate();
        return database;
    }

    [Fact]
    public async Task Enqueue_CompletesDownload_AndPersists()
    {
        using var db = NewDatabase(out var dbPath);
        await using var manager = new DownloadManager(new DownloadsRepository(db), Options, () => new HttpClient());
        var url = _server.Url + "file.bin";

        var task = manager.Enqueue("local:movie:1", "local", "Local Film", null, url, "local-film.bin");
        var completed = await WaitForAsync(() => task.Status == DownloadStatus.Completed);

        Assert.True(completed, $"download did not complete; status={task.Status} error={task.ErrorMessage}");
        Assert.Equal(_server.Payload.LongLength, task.BytesDownloaded);
        Assert.Equal(_server.Payload.LongLength, task.TotalBytes);
        Assert.True(File.Exists(task.DestinationPath));

        using var db2 = new Database(dbPath);
        var persisted = new DownloadsRepository(db2).GetAll().Single(t => t.Id == task.Id);
        Assert.Equal(DownloadStatus.Completed, persisted.Status);
    }

    [Fact]
    public async Task Enqueue_DuplicateItem_IsRejected()
    {
        using var db = NewDatabase(out _);
        await using var manager = new DownloadManager(new DownloadsRepository(db), Options, () => new HttpClient());
        var url = _server.Url + "file.bin";

        var first = manager.Enqueue("local:movie:1", "local", "Local Film", null, url, "dup.bin");
        await WaitForAsync(() => first.Status is DownloadStatus.Downloading or DownloadStatus.Completed);

        // Duplicate while the first is still active is rejected...
        if (first.Status != DownloadStatus.Completed)
        {
            Assert.Throws<InvalidOperationException>(
                () => manager.Enqueue("local:movie:1", "local", "Local Film", null, url, "dup2.bin"));
        }

        // ...but re-downloading a completed item is allowed.
        await WaitForAsync(() => first.Status == DownloadStatus.Completed);
        var again = manager.Enqueue("local:movie:1", "local", "Local Film", null, url, "dup2.bin");
        Assert.NotSame(first, again);
    }

    [Fact]
    public async Task PauseAndResume_ResumeWithRange()
    {
        using var db = NewDatabase(out _);
        await using var manager = new DownloadManager(new DownloadsRepository(db), Options, () => new HttpClient());
        var url = _server.Url + "file.bin";

        var task = manager.Enqueue("local:show:1", "local", "Show", "S1 E1", url, "show-e1.bin");

        // Pause after at least one progress tick has flushed.
        await WaitForAsync(() => task.Status == DownloadStatus.Downloading && task.BytesDownloaded > 0);
        manager.Pause(task.Id);
        await WaitForAsync(() => task.Status == DownloadStatus.Paused);
        var pausedBytes = task.BytesDownloaded;
        Assert.True(pausedBytes > 0, "pause happened before any bytes arrived; retry");

        manager.Resume(task.Id);
        await WaitForAsync(() => task.Status == DownloadStatus.Completed);

        Assert.Equal(_server.Payload.LongLength, task.BytesDownloaded);
        Assert.True(File.Exists(task.DestinationPath));
        Assert.Equal(_server.Payload.LongLength, new FileInfo(task.DestinationPath).Length);
    }

    [Fact]
    public async Task Delete_RemovesTask_AndStopsDownload()
    {
        using var db = NewDatabase(out _);
        await using var manager = new DownloadManager(new DownloadsRepository(db), Options, () => new HttpClient());
        var url = _server.Url + "file.bin";

        var task = manager.Enqueue("local:show:2", "local", "Show", "S1 E2", url, "show-e2.bin");
        await WaitForAsync(() => task.Status == DownloadStatus.Downloading);

        manager.Delete(task.Id);
        Assert.DoesNotContain(manager.Tasks, t => t.Id == task.Id);
    }

    [Fact]
    public async Task CompletedFile_MatchesServerPayload()
    {
        using var db = NewDatabase(out _);
        await using var manager = new DownloadManager(new DownloadsRepository(db), Options, () => new HttpClient());

        var task = manager.Enqueue("local:movie:2", "local", "Payload Film", null, _server.Url + "payload.bin", "payload.bin");
        await WaitForAsync(() => task.Status == DownloadStatus.Completed);

        Assert.Equal(_server.Payload, await File.ReadAllBytesAsync(task.DestinationPath));
    }

    private static async Task<bool> WaitForAsync(Func<bool> condition, int timeoutMs = 15000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return true;
            }

            await Task.Delay(50);
        }

        return condition();
    }
}
