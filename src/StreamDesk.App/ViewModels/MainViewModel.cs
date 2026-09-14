using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using StreamDesk.Application.Managers;
using StreamDesk.Application.Services;
using StreamDesk.Core;

namespace StreamDesk.App.ViewModels;

/// <summary>Which page is currently displayed in the shell.</summary>
public enum AppPage
{
    Home,
    Movies,
    TvShows,
    Anime,
    LiveTv,
    Downloads,
    Favorites,
    Settings,
    Search,
    Details
}

/// <summary>Shell view model: sidebar navigation + shared state (search, details, help).</summary>
public sealed class MainViewModel : ViewModelBase
{
    private readonly SimpleContainer _services;
    private AppPage _currentPage = AppPage.Home;
    private ViewModelBase _currentPageViewModel;
    private string _searchText = string.Empty;
    private DetailsViewModel? _details;
    private bool _isDetailsOpen;
    private bool _isNavExpanded = true;
    private bool _isHelpOpen;

    public MainViewModel(SimpleContainer services)
    {
        _services = services;
        _currentPageViewModel = PageFor(AppPage.Home);
        NavigateHomeCommand = new RelayCommand(() => Navigate(AppPage.Home));
        NavigateMoviesCommand = new RelayCommand(() => Navigate(AppPage.Movies));
        NavigateShowsCommand = new RelayCommand(() => Navigate(AppPage.TvShows));
        NavigateAnimeCommand = new RelayCommand(() => Navigate(AppPage.Anime));
        NavigateLiveTvCommand = new RelayCommand(() => Navigate(AppPage.LiveTv));
        NavigateDownloadsCommand = new RelayCommand(() => Navigate(AppPage.Downloads));
        NavigateFavoritesCommand = new RelayCommand(() => Navigate(AppPage.Favorites));
        NavigateSettingsCommand = new RelayCommand(() => Navigate(AppPage.Settings));
        ToggleNavCommand = new RelayCommand(() => IsNavExpanded = !IsNavExpanded);
        ToggleHelpCommand = new RelayCommand(() => IsHelpOpen = !IsHelpOpen);

        _ = ((HomeViewModel)CurrentPageViewModel).LoadAsync();
    }

    public RelayCommand NavigateHomeCommand { get; }

    public RelayCommand NavigateMoviesCommand { get; }

    public RelayCommand NavigateShowsCommand { get; }

    public RelayCommand NavigateAnimeCommand { get; }

    public RelayCommand NavigateLiveTvCommand { get; }

    public RelayCommand NavigateDownloadsCommand { get; }

    public RelayCommand NavigateFavoritesCommand { get; }

    public RelayCommand NavigateSettingsCommand { get; }

    public RelayCommand ToggleNavCommand { get; }

    public RelayCommand ToggleHelpCommand { get; }

    public HomeViewModel Home => (HomeViewModel)PageFor(AppPage.Home);

    public SearchViewModel Search => (SearchViewModel)PageFor(AppPage.Search);

    public LiveTvViewModel LiveTv => (LiveTvViewModel)PageFor(AppPage.LiveTv);

    public DownloadsViewModel Downloads => (DownloadsViewModel)PageFor(AppPage.Downloads);

    public FavoritesViewModel Favorites => (FavoritesViewModel)PageFor(AppPage.Favorites);

    public SettingsViewModel Settings => (SettingsViewModel)PageFor(AppPage.Settings);

    public DetailsViewModel? Details
    {
        get => _details;
        private set => SetProperty(ref _details, value);
    }

    public bool IsDetailsOpen
    {
        get => _isDetailsOpen;
        private set => SetProperty(ref _isDetailsOpen, value);
    }

    public AppPage CurrentPage
    {
        get => _currentPage;
        private set
        {
            if (SetProperty(ref _currentPage, value))
            {
                CurrentPageViewModel = PageFor(value);
                OnPageNavigated(value);
            }
        }
    }

    public ViewModelBase CurrentPageViewModel
    {
        get => _currentPageViewModel;
        private set => SetProperty(ref _currentPageViewModel, value);
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value) && value.Trim().Length >= 2)
            {
                Search.Query = value;
                Navigate(AppPage.Search);
            }
        }
    }

    public bool IsNavExpanded
    {
        get => _isNavExpanded;
        set => SetProperty(ref _isNavExpanded, value);
    }

    public bool IsHelpOpen
    {
        get => _isHelpOpen;
        set => SetProperty(ref _isHelpOpen, value);
    }

    /// <summary>Open the details overlay for a catalog card.</summary>
    public async void OpenDetails(MediaCardViewModel card)
    {
        try
        {
            var manager = _services.GetRequiredService<ProviderManager>();
            var details = await manager.GetDetailsAsync(card.Summary.Id, card.Summary.ProviderId).ConfigureAwait(true);
            if (details is null)
            {
                IsHelpOpen = false;
                return;
            }

            var vm = new DetailsViewModel(_services);
            vm.Load(details);
            Details = vm;
            IsDetailsOpen = true;
        }
        catch (Exception)
        {
            // Details unavailable; overlay stays closed.
        }
    }

    public void OpenChannel(ChannelItemViewModel channel)
    {
        // Channels play directly via the M3U provider.
        var playback = _services.GetRequiredService<IPlaybackService>();
        var source = new PlaybackSource(PlaybackSourceKind.NetworkStream, channel.StreamUrl)
        {
            Title = channel.Name
        };
        _ = playback.PlayAsync(source);
    }

    public void CloseDetails()
    {
        IsDetailsOpen = false;
        Details = null;
    }

    public void Navigate(AppPage page) => CurrentPage = page;

    public void NavigateHome() => Navigate(AppPage.Home);

    public void NavigateToLiveTv() => Navigate(AppPage.LiveTv);

    public void NavigateToDownloads() => Navigate(AppPage.Downloads);

    public void NavigateToSettings() => Navigate(AppPage.Settings);

    private void OnPageNavigated(AppPage page)
    {
        switch (page)
        {
            case AppPage.Home:
                _ = Home.LoadAsync();
                break;
            case AppPage.LiveTv:
                _ = LiveTv.LoadAsync();
                break;
            case AppPage.Downloads:
                Downloads.Refresh();
                break;
            case AppPage.Favorites:
                Favorites.Reload();
                break;
        }
    }

    private ViewModelBase PageFor(AppPage page) => page switch
    {
        AppPage.Home => _home ??= new HomeViewModel(_services),
        AppPage.Movies => _movies ??= new BrowseViewModel(_services, "Movies", MediaType.Movie),
        AppPage.TvShows => _tvShows ??= new BrowseViewModel(_services, "TV Shows", MediaType.Show),
        AppPage.Anime => _anime ??= new BrowseViewModel(_services, "Anime", MediaType.Show),
        AppPage.LiveTv => _liveTv ??= new LiveTvViewModel(_services),
        AppPage.Downloads => _downloads ??= new DownloadsViewModel(_services),
        AppPage.Favorites => _favorites ??= new FavoritesViewModel(_services),
        AppPage.Settings => _settings ??= new SettingsViewModel(_services),
        AppPage.Search => _search ??= new SearchViewModel(_services),
        AppPage.Details => CurrentPageViewModel,
        _ => throw new ArgumentOutOfRangeException(nameof(page))
    };

    private HomeViewModel? _home;
    private BrowseViewModel? _movies;
    private BrowseViewModel? _tvShows;
    private BrowseViewModel? _anime;
    private LiveTvViewModel? _liveTv;
    private DownloadsViewModel? _downloads;
    private FavoritesViewModel? _favorites;
    private SettingsViewModel? _settings;
    private SearchViewModel? _search;
}

/// <summary>Minimal synchronous command.</summary>
public sealed class RelayCommand : System.Windows.Input.ICommand
{
    private readonly Action _execute;

    public RelayCommand(Action execute)
    {
        _execute = execute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => _execute();
}
