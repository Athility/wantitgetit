using System;
using System.IO;
using System.Net.Http;
using StreamDesk.Application.Managers;
using StreamDesk.Application.Providers;
using StreamDesk.Application.Services;
using StreamDesk.Core;
using StreamDesk.Infrastructure;
using StreamDesk.Infrastructure.Database;
using StreamDesk.Infrastructure.Networking;
using StreamDesk.Infrastructure.Storage;
using StreamDesk.Providers.FourKHDHub;
using StreamDesk.Providers.Local;
using StreamDesk.Providers.M3U;
using StreamDesk.Providers.MovieBox;
using StreamDesk.Providers.Tmdb;

namespace StreamDesk.App;

/// <summary>Composition root: constructs and wires every service and provider.</summary>
public static class Composition
{
    public static SimpleContainer Build()
    {
        var database = new Database(AppPaths.DatabasePath);
        database.Migrate();

        var settingsStore = new AppSettingsStore(new SettingsRepository(database));
        var settings = new SettingsService(settingsStore);
        settings.Load();

        var favorites = new FavoritesService(new FavoritesRepository(database));
        var history = new WatchHistoryService(new WatchHistoryRepository(database));
        var subtitleService = new SubtitleService();
        var playback = new PlaybackService(settings);

        var playlistsRepo = new PlaylistsRepository(database);
        var channelsRepo = new ChannelsRepository(database);
        var foldersRepo = new LibraryFoldersRepository(database);
        var libraryRepo = new MediaLibraryRepository(database);
        var libraryService = new LocalLibraryService(foldersRepo, libraryRepo);

        Func<TmdbConfiguration> tmdbConfig = () => new TmdbConfiguration
        {
            ApiKey = SecretProtector.Unprotect(settings.Current.TmdbApiKey)
        };

        Func<MovieBoxConfiguration> movieBoxConfig = () => new MovieBoxConfiguration
        {
            BaseUrl = SecretProtector.Unprotect(settings.Current.MovieBoxBaseUrl),
            ApiKey = SecretProtector.Unprotect(settings.Current.MovieBoxApiKey)
        };

        Func<FourKHDHubConfiguration> fourKConfig = () => new FourKHDHubConfiguration
        {
            BaseUrl = SecretProtector.Unprotect(settings.Current.FourKHDHubBaseUrl),
            ApiKey = SecretProtector.Unprotect(settings.Current.FourKHDHubApiKey)
        };

        var manager = new ProviderManager(providerId => providerId switch
        {
            ProviderIds.Tmdb => settings.Current.ProviderEnabled_Tmdb,
            ProviderIds.Local => settings.Current.ProviderEnabled_Local,
            ProviderIds.M3U => settings.Current.ProviderEnabled_M3U,
            ProviderIds.MovieBox => settings.Current.ProviderEnabled_MovieBox,
            ProviderIds.FourKHDHub => settings.Current.ProviderEnabled_FourKHDHub,
            _ => true
        });

        manager.Register(new TmdbProvider(tmdbConfig));
        manager.Register(new LocalMediaProvider(libraryRepo, foldersRepo));
        manager.Register(new M3UProvider(playlistsRepo, channelsRepo));
        manager.Register(new MovieBoxProvider(movieBoxConfig));
        manager.Register(new FourKHDHubProvider(fourKConfig));
        _ = manager.InitializeAllAsync();

        if (string.IsNullOrWhiteSpace(settings.Current.DownloadDirectory))
        {
            settings.Current.DownloadDirectory = AppPaths.DownloadDirectory;
        }

        var downloads = new DownloadManager(
            new DownloadsRepository(database),
            () => new DownloadManagerOptions
            {
                DownloadDirectory = settings.Current.DownloadDirectory,
                MaxConcurrentDownloads = Math.Clamp(settings.Current.ConcurrentDownloads, 1, 6),
                AllowResume = settings.Current.ResumeDownloads
            },
            () => HttpClientFactory.Create("StreamDesk-Download/1.0"));

        return new SimpleContainer()
            .Register(database)
            .Register<ISettingsService>(settings)
            .Register<IFavoritesService>(favorites)
            .Register<IWatchHistoryService>(history)
            .Register<ISubtitleService>(subtitleService)
            .Register<IPlaybackService>(playback)
            .Register(libraryService)
            .Register(playlistsRepo)
            .Register(channelsRepo)
            .Register(foldersRepo)
            .Register(libraryRepo)
            .Register(manager)
            .Register(downloads)
            .Register<ImageCache>(new ImageCache(HttpClientFactory.Create("StreamDesk-Images/1.0"), AppPaths.ImageCacheDirectory))
            .Register(new Func<HttpClient>(() => HttpClientFactory.Create()));
    }
}
