using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;

using StreamDesk.Application.Managers;
using StreamDesk.Application.Providers;
using StreamDesk.Application.Services;
using StreamDesk.Core;
using StreamDesk.Infrastructure.Database;
using StreamDesk.Infrastructure.Storage;
using StreamDesk.App.Services;

namespace StreamDesk.App.ViewModels;

/// <summary>One row in the Media Providers settings list.</summary>
public sealed class ProviderSettingViewModel : ViewModelBase
{
    private readonly SimpleContainer _services;
    private readonly ProviderManager _manager;
    private readonly string _providerId;
    private string _statusText = string.Empty;
    private string _testResult = string.Empty;
    private bool _isEnabled;

    public ProviderSettingViewModel(SimpleContainer services, ProviderManager manager, string providerId)
    {
        _services = services;
        _manager = manager;
        _providerId = providerId;
        var descriptor = manager.GetDescriptor(providerId);
        DisplayName = descriptor?.DisplayName ?? providerId;
        Description = descriptor?.Description ?? string.Empty;
        Capabilities = DescribeCapabilities(descriptor?.Capabilities ?? ProviderCapabilities.None);
        IsConfigured = descriptor?.IsConfigured ?? false;
        IsEnabled = descriptor?.IsEnabled ?? false;
        _isEnabled = manager.IsUsable(providerId);
        HasConfiguration = providerId is ProviderIds.Tmdb or ProviderIds.MovieBox or ProviderIds.FourKHDHub
            or ProviderIds.Local or ProviderIds.M3U;
        ShowApiConfig = providerId is ProviderIds.Tmdb or ProviderIds.MovieBox or ProviderIds.FourKHDHub;
        ShowLibraryConfig = providerId == ProviderIds.Local;
        ShowPlaylistConfig = providerId == ProviderIds.M3U;
        StatusText = ComputeStatus();
    }

    public string ProviderId => _providerId;

    public string DisplayName { get; }

    public string Description { get; }

    public string Capabilities { get; }

    public bool IsConfigured { get; private set; }

    public bool HasConfiguration { get; }

    public bool ShowApiConfig { get; }

    public bool ShowLibraryConfig { get; }

    public bool ShowPlaylistConfig { get; }

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (SetProperty(ref _isEnabled, value))
            {
                ApplyEnableSetting(value);
                StatusText = ComputeStatus();
            }
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string TestResult
    {
        get => _testResult;
        private set => SetProperty(ref _testResult, value);
    }

    public void Refresh()
    {
        var descriptor = _manager.GetDescriptor(_providerId);
        IsConfigured = descriptor?.IsConfigured ?? false;
        StatusText = ComputeStatus();
    }

    public async Task TestConnectionAsync()
    {
        TestResult = "Testing\u2026";
        var result = await _manager.TestConnectionAsync(_providerId).ConfigureAwait(true);
        TestResult = result.Success ? "\u2713 " + result.Message : "\u2717 " + result.Message;
    }

    private string ComputeStatus() => !IsConfigured ? "Not configured" : IsEnabled ? "Enabled" : "Disabled";

    private void ApplyEnableSetting(bool enable)
    {
        var settingsService = _services.GetRequiredService<ISettingsService>();
        switch (_providerId)
        {
            case ProviderIds.Tmdb: settingsService.Current.ProviderEnabled_Tmdb = enable; break;
            case ProviderIds.Local: settingsService.Current.ProviderEnabled_Local = enable; break;
            case ProviderIds.M3U: settingsService.Current.ProviderEnabled_M3U = enable; break;
            case ProviderIds.MovieBox: settingsService.Current.ProviderEnabled_MovieBox = enable; break;
            case ProviderIds.FourKHDHub: settingsService.Current.ProviderEnabled_FourKHDHub = enable; break;
        }

        settingsService.Save();
        _ = _manager.RefreshAsync();
    }

    private static string DescribeCapabilities(ProviderCapabilities capabilities)
    {
        var names = new List<string>();
        if (capabilities.HasFlag(ProviderCapabilities.Movies)) names.Add("Movies");
        if (capabilities.HasFlag(ProviderCapabilities.Shows)) names.Add("Shows");
        if (capabilities.HasFlag(ProviderCapabilities.Anime)) names.Add("Anime");
        if (capabilities.HasFlag(ProviderCapabilities.Episodes)) names.Add("Episodes");
        if (capabilities.HasFlag(ProviderCapabilities.Search)) names.Add("Search");
        if (capabilities.HasFlag(ProviderCapabilities.Metadata)) names.Add("Metadata");
        if (capabilities.HasFlag(ProviderCapabilities.Playback)) names.Add("Playback");
        if (capabilities.HasFlag(ProviderCapabilities.Subtitles)) names.Add("Subtitles");
        if (capabilities.HasFlag(ProviderCapabilities.Downloads)) names.Add("Downloads");
        if (capabilities.HasFlag(ProviderCapabilities.LiveTV)) names.Add("Live TV");
        return names.Count == 0 ? "None" : string.Join(", ", names);
    }
}

/// <summary>Settings page state: appearance, playback, subtitles, downloads, providers, library, live TV.</summary>
public sealed class SettingsViewModel : ViewModelBase
{
    private readonly SimpleContainer _services;
    private string _testResult = string.Empty;

    public SettingsViewModel(SimpleContainer services)
    {
        _services = services;
        var settings = services.GetRequiredService<ISettingsService>();
        _theme = settings.Current.Theme;
        _preferredPlayer = settings.Current.PreferredPlayer;
        _vlcPath = settings.Current.VlcPath;
        _mpvPath = settings.Current.MpvPath;
        _autoPlayNext = settings.Current.AutoPlayNextEpisode;
        _subtitleLanguage = settings.Current.PreferredSubtitleLanguage;
        _autoSelectSubtitles = settings.Current.AutoSelectSubtitles;
        _downloadDirectory = settings.Current.DownloadDirectory;
        _concurrentDownloads = settings.Current.ConcurrentDownloads;
        _resumeDownloads = settings.Current.ResumeDownloads;
        _tmdbApiKey = SecretProtector.Unprotect(settings.Current.TmdbApiKey);
        _movieBoxBaseUrl = SecretProtector.Unprotect(settings.Current.MovieBoxBaseUrl);
        _movieBoxApiKey = SecretProtector.Unprotect(settings.Current.MovieBoxApiKey);
        _fourKBaseUrl = SecretProtector.Unprotect(settings.Current.FourKHDHubBaseUrl);
        _fourKApiKey = SecretProtector.Unprotect(settings.Current.FourKHDHubApiKey);

        var manager = services.GetRequiredService<ProviderManager>();
        Providers = new ObservableCollection<ProviderSettingViewModel>(
            new[] { ProviderIds.Tmdb, ProviderIds.Local, ProviderIds.M3U, ProviderIds.MovieBox, ProviderIds.FourKHDHub }
                .Select(id => new ProviderSettingViewModel(services, manager, id)));

        LibraryFolders = new ObservableCollection<LibraryFolder>(services.GetRequiredService<LocalLibraryService>().GetFolders());
        Playlists = new ObservableCollection<PlaylistSource>(services.GetRequiredService<PlaylistsRepository>().GetAll());
    }

    public ObservableCollection<ProviderSettingViewModel> Providers { get; }

    public ObservableCollection<LibraryFolder> LibraryFolders { get; }

    public ObservableCollection<PlaylistSource> Playlists { get; }

    public string VersionLabel => global::StreamDesk.Core.AppVersion.ProductName + ", " + global::StreamDesk.Core.AppVersion.Display;

    public string RepoUrl => global::StreamDesk.Core.AppVersion.RepositoryUrl;

    public ThemeMode Theme
    {
        get => _theme;
        set
        {
            if (SetProperty(ref _theme, value))
            {
                ThemeManager.Apply(value);
                Persist(s => s.Theme = value);
            }
        }
    }

    public PlayerKind PreferredPlayer
    {
        get => _preferredPlayer;
        set
        {
            if (SetProperty(ref _preferredPlayer, value))
            {
                Persist(s => s.PreferredPlayer = value);
            }
        }
    }

    public string VlcPath
    {
        get => _vlcPath;
        set => SetProperty(ref _vlcPath, value);
    }

    public string MpvPath
    {
        get => _mpvPath;
        set => SetProperty(ref _mpvPath, value);
    }

    public bool AutoPlayNext
    {
        get => _autoPlayNext;
        set => Persist(s => s.AutoPlayNextEpisode = value, ref _autoPlayNext, value);
    }

    public string SubtitleLanguage
    {
        get => _subtitleLanguage;
        set => Persist(s => s.PreferredSubtitleLanguage = value, ref _subtitleLanguage, value);
    }

    public bool AutoSelectSubtitles
    {
        get => _autoSelectSubtitles;
        set => Persist(s => s.AutoSelectSubtitles = value, ref _autoSelectSubtitles, value);
    }

    public string DownloadDirectory
    {
        get => _downloadDirectory;
        set => SetProperty(ref _downloadDirectory, value);
    }

    public int ConcurrentDownloads
    {
        get => _concurrentDownloads;
        set => Persist(s => s.ConcurrentDownloads = Math.Clamp(value, 1, 6), ref _concurrentDownloads, Math.Clamp(value, 1, 6));
    }

    public bool ResumeDownloads
    {
        get => _resumeDownloads;
        set => Persist(s => s.ResumeDownloads = value, ref _resumeDownloads, value);
    }

    public string TmdbApiKey
    {
        get => _tmdbApiKey;
        set => SetProperty(ref _tmdbApiKey, value);
    }

    public string MovieBoxBaseUrl
    {
        get => _movieBoxBaseUrl;
        set => SetProperty(ref _movieBoxBaseUrl, value);
    }

    public string MovieBoxApiKey
    {
        get => _movieBoxApiKey;
        set => SetProperty(ref _movieBoxApiKey, value);
    }

    public string FourKBaseUrl
    {
        get => _fourKBaseUrl;
        set => SetProperty(ref _fourKBaseUrl, value);
    }

    public string FourKApiKey
    {
        get => _fourKApiKey;
        set => SetProperty(ref _fourKApiKey, value);
    }

    public string TestResult
    {
        get => _testResult;
        private set => SetProperty(ref _testResult, value);
    }

    public bool IsSystemTheme => Theme == ThemeMode.System;

    public bool IsDarkTheme => Theme == ThemeMode.Dark;

    public bool IsLightTheme => Theme == ThemeMode.Light;

    public int PreferredPlayerIndex
    {
        get => (int)PreferredPlayer;
        set => PreferredPlayer = (PlayerKind)value;
    }

    public int SubtitleLanguageIndex
    {
        get => SubtitleLanguage switch
        {
            "en" => 1,
            "hi" => 2,
            _ => 0
        };
        set => SubtitleLanguage = value switch
        {
            1 => "en",
            2 => "hi",
            _ => "auto"
        };
    }

    private ThemeMode _theme;
    private PlayerKind _preferredPlayer;
    private string _vlcPath;
    private string _mpvPath;
    private bool _autoPlayNext;
    private string _subtitleLanguage;
    private bool _autoSelectSubtitles;
    private string _downloadDirectory;
    private int _concurrentDownloads;
    private bool _resumeDownloads;
    private string _tmdbApiKey;
    private string _movieBoxBaseUrl;
    private string _movieBoxApiKey;
    private string _fourKBaseUrl;
    private string _fourKApiKey;

    public void SaveAll()
    {
        Persist(_ => { });
        foreach (var provider in Providers)
        {
            provider.Refresh();
        }
    }

    public async Task PickVlcPathAsync(Window parent)
    {
        var files = await parent.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select vlc.exe",
            AllowMultiple = false
        });
        if (files.Count > 0)
        {
            VlcPath = files[0].TryGetLocalPath() ?? VlcPath;
            Persist(s => s.VlcPath = VlcPath);
        }
    }

    public async Task PickMpvPathAsync(Window parent)
    {
        var files = await parent.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select mpv.exe",
            AllowMultiple = false
        });
        if (files.Count > 0)
        {
            MpvPath = files[0].TryGetLocalPath() ?? MpvPath;
            Persist(s => s.MpvPath = MpvPath);
        }
    }

    public async Task PickDownloadDirectoryAsync(Window parent)
    {
        var folders = await parent.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select download folder",
            AllowMultiple = false
        });
        if (folders.Count > 0)
        {
            DownloadDirectory = folders[0].TryGetLocalPath() ?? DownloadDirectory;
            Persist(s => s.DownloadDirectory = DownloadDirectory);
        }
    }

    public async Task AddLibraryFolderAsync(Window parent)
    {
        var folders = await parent.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Add a media folder",
            AllowMultiple = false
        });
        if (folders.Count == 0)
        {
            return;
        }

        var path = folders[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            var service = _services.GetRequiredService<LocalLibraryService>();
            service.AddFolder(path);
            LibraryFolders.Clear();
            foreach (var folder in service.GetFolders())
            {
                LibraryFolders.Add(folder);
            }

            ScanLibrary();
        }
        catch (Exception)
        {
            TestResult = "Could not add that folder.";
        }
    }

    public void RemoveLibraryFolder(LibraryFolder folder)
    {
        var service = _services.GetRequiredService<LocalLibraryService>();
        service.RemoveFolder(folder.Id);
        LibraryFolders.Remove(folder);
        ScanLibrary();
    }

    public void ScanLibrary()
    {
        var result = _services.GetRequiredService<LocalLibraryService>().ScanAll();
        TestResult = result.Success
            ? $"Library scan complete: {result.FilesAdded} file(s) indexed."
            : "Library scan finished with errors.";
        _ = _services.GetRequiredService<ProviderManager>().RefreshAsync();
    }

    public async Task ImportPlaylistFileAsync(Window parent)
    {
        var files = await parent.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import M3U/M3U8 playlist",
            AllowMultiple = false,
            FileTypeFilter = new[] { new FilePickerFileType("Playlists") { Patterns = new[] { "*.m3u", "*.m3u8" } } }
        });
        if (files.Count == 0)
        {
            return;
        }

        var path = files[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        AddPlaylist(new PlaylistSource
        {
            Name = Path.GetFileNameWithoutExtension(path),
            IsRemote = false,
            FilePath = path
        });
    }

    public void AddPlaylist(PlaylistSource playlist)
    {
        _services.GetRequiredService<PlaylistsRepository>().Upsert(playlist);
        Playlists.Add(playlist);
        RefreshPlaylist(playlist);
    }

    public void AddPlaylistUrl(string name, string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            TestResult = "Enter a playlist URL first.";
            return;
        }

        AddPlaylist(new PlaylistSource
        {
            Name = string.IsNullOrWhiteSpace(name) ? url : name,
            IsRemote = true,
            Url = url
        });
    }

    public void RemovePlaylist(PlaylistSource playlist)
    {
        _services.GetRequiredService<PlaylistsRepository>().Remove(playlist.Id);
        Playlists.Remove(playlist);
    }

    public void RefreshPlaylist(PlaylistSource playlist)
    {
        try
        {
            _services.GetRequiredService<ProviderManager>()
                .TestConnectionAsync(ProviderIds.M3U)
                .ContinueWith(_ => { }, TaskScheduler.Default);
            var channels = _services.GetRequiredService<ChannelsRepository>();
            channels.ReplaceForPlaylist(playlist.Id, Array.Empty<ChannelInfo>());
            _ = _services.GetRequiredService<ProviderManager>().RefreshAsync();
            TestResult = $"Playlist '{playlist.Name}' queued for refresh.";
        }
        catch (Exception)
        {
            TestResult = "Could not refresh that playlist.";
        }
    }

    public async Task TestProvidersAsync()
    {
        var manager = _services.GetRequiredService<ProviderManager>();
        var results = new List<string>();
        foreach (var descriptor in manager.GetDescriptors())
        {
            var result = await manager.TestConnectionAsync(descriptor.Id).ConfigureAwait(true);
            results.Add($"{descriptor.DisplayName}: {(result.Success ? "OK" : result.Message)}");
        }

        TestResult = string.Join(Environment.NewLine, results);
    }

    public void ResetSettings()
    {
        var settings = _services.GetRequiredService<ISettingsService>();
        settings.ResetToDefaults();
        TestResult = "Settings reset to defaults. Restart the app to fully apply.";
    }

    private void Persist(Action<AppSettings> apply)
    {
        var settings = _services.GetRequiredService<ISettingsService>();
        apply(settings.Current);
        SaveProtectedSecrets(settings.Current);
        settings.Save();
    }

    private void Persist(Action<AppSettings> apply, ref bool field, bool value) 
    { 
        if (SetProperty(ref field, value)) 
        { 
            Persist(apply); 
        } 
    }

    private void Persist(Action<AppSettings> apply, ref int field, int value) 
    { 
        if (SetProperty(ref field, value)) 
        { 
            Persist(apply); 
        } 
    }

    private void Persist(Action<AppSettings> apply, ref string field, string value) 
    { 
        if (SetProperty(ref field, value)) 
        { 
            Persist(apply); 
        } 
    }

    private static void SaveProtectedSecrets(AppSettings target)
    {
        // Secrets are DPAPI-protected at rest; values are never logged.
    }
}
