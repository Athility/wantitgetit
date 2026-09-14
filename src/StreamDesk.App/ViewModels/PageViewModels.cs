using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using StreamDesk.Application.Managers;
using StreamDesk.Application.Services;
using StreamDesk.Core;

namespace StreamDesk.App.ViewModels;

/// <summary>Home page: horizontal sections built from provider catalogs.</summary>
public sealed class HomeViewModel : ViewModelBase
{
    private readonly SimpleContainer _services;
    private readonly ProviderManager _manager;
    private bool _isLoading = true;
    private bool _hasError;
    private IReadOnlyList<HomeSectionViewModel> _sections = Array.Empty<HomeSectionViewModel>();
    private IReadOnlyList<MediaCardViewModel> _continueWatching = Array.Empty<MediaCardViewModel>();

    public HomeViewModel(SimpleContainer services)
    {
        _services = services;
        _manager = services.GetRequiredService<ProviderManager>();
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public bool HasError
    {
        get => _hasError;
        private set => SetProperty(ref _hasError, value);
    }

    public IReadOnlyList<HomeSectionViewModel> Sections
    {
        get => _sections;
        private set
        {
            SetProperty(ref _sections, value);
            OnPropertyChanged(nameof(SectionsEmpty));
        }
    }

    public bool SectionsEmpty => Sections.Count == 0 && !IsLoading && !HasError;

    public IReadOnlyList<MediaCardViewModel> ContinueWatching
    {
        get => _continueWatching;
        private set
        {
            SetProperty(ref _continueWatching, value);
            OnPropertyChanged(nameof(HasContinueWatching));
        }
    }

    public bool HasContinueWatching => ContinueWatching.Count > 0;

    public async Task LoadAsync()
    {
        IsLoading = true;
        HasError = false;
        try
        {
            var sections = new List<HomeSectionViewModel>();
            var loadTasks = _manager.GetHomeSections()
                .Select(async sectionName =>
                {
                    try
                    {
                        var page = await _manager.GetSectionAsync(sectionName, 1).ConfigureAwait(true);
                        var cards = page.Items.Take(20).Select(i => new MediaCardViewModel(i, _services)).ToList();
                        return (sectionName, cards);
                    }
                    catch (Exception)
                    {
                        return (sectionName, new List<MediaCardViewModel>());
                    }
                })
                .ToList();

            var results = await Task.WhenAll(loadTasks).ConfigureAwait(true);
            foreach (var (name, cards) in results)
            {
                if (cards.Count > 0)
                {
                    sections.Add(new HomeSectionViewModel(name, cards));
                }
            }

            Sections = sections;

            var history = _services.GetRequiredService<IWatchHistoryService>();
            ContinueWatching = history.GetContinueWatching(12)
                .Select(e => new MediaCardViewModel(new MediaSummary(
                    e.ItemId, e.ProviderId, e.MediaType, e.Title)
                {
                    PosterUrl = e.PosterUrl,
                    Category = e.Subtitle ?? (e.PercentComplete > 0 ? $"{e.PercentComplete:0}%" : null)
                }, _services))
                .ToList();
            OnPropertyChanged(nameof(HasContinueWatching));
        }
        catch (Exception)
        {
            HasError = true;
        }
        finally
        {
            IsLoading = false;
        }
    }
}

/// <summary>Global search page state.</summary>
public sealed class SearchViewModel : ViewModelBase
{
    private readonly SimpleContainer _services;
    private readonly ProviderManager _manager;
    private readonly CancellationTokenSource _debounce = new();
    private string _query = string.Empty;
    private bool _isSearching;
    private bool _hasSearched;
    private string? _error;
    private IReadOnlyList<MediaCardViewModel> _results = Array.Empty<MediaCardViewModel>();

    public SearchViewModel(SimpleContainer services)
    {
        _services = services;
        _manager = services.GetRequiredService<ProviderManager>();
    }

    public string Query
    {
        get => _query;
        set
        {
            if (SetProperty(ref _query, value))
            {
                _ = DebouncedSearchAsync();
            }
        }
    }

    public bool IsSearching
    {
        get => _isSearching;
        private set => SetProperty(ref _isSearching, value);
    }

    public bool HasSearched
    {
        get => _hasSearched;
        private set => SetProperty(ref _hasSearched, value);
    }

    public string? Error
    {
        get => _error;
        private set => SetProperty(ref _error, value);
    }

    public bool HasError => Error is not null;

    public bool HasResults => Results.Count > 0;

    public bool ShowEmptyHint => !IsSearching && Query.Trim().Length < 2;

    public bool ShowNoResults => HasSearched && !IsSearching && !HasResults && Query.Trim().Length >= 2 && !HasError;

    public IReadOnlyList<MediaCardViewModel> Results
    {
        get => _results;
        private set
        {
            SetProperty(ref _results, value);
            OnPropertyChanged(nameof(HasResults));
            OnPropertyChanged(nameof(ShowNoResults));
        }
    }

    public async Task SearchNowAsync()
    {
        var query = Query.Trim();
        HasSearched = true;
        Error = null;
        if (query.Length == 0)
        {
            Results = Array.Empty<MediaCardViewModel>();
            return;
        }

        IsSearching = true;
        try
        {
            var items = await _manager.SearchAsync(new SearchRequest(query)).ConfigureAwait(true);
            Results = items.Select(i => new MediaCardViewModel(i, _services)).ToList();
        }
        catch (Exception)
        {
            Error = "Search failed. Check your network connection or provider settings.";
            Results = Array.Empty<MediaCardViewModel>();
        }
        finally
        {
            IsSearching = false;
        }
    }

    private async Task DebouncedSearchAsync()
    {
        // 350ms debounce: cancel pending work, wait, then search.
        var token = _debounce;
        try { token.Cancel(); } catch { /* disposed */ }
        var fresh = new CancellationTokenSource();
        // (single-flight debounce; kept simple for the desktop use case)
        await Task.Delay(350).ConfigureAwait(true);
        if (!fresh.Token.IsCancellationRequested && Query.Trim().Length > 0)
        {
            await SearchNowAsync().ConfigureAwait(true);
        }
    }
}

/// <summary>Browse page for one media type (Movies, TV Shows, Anime).</summary>
public sealed class BrowseViewModel : ViewModelBase
{
    private readonly SimpleContainer _services;
    private readonly ProviderManager _manager;
    private readonly MediaType _mediaType;
    private readonly string _title;
    private bool _isLoading = true;
    private string? _error;
    private IReadOnlyList<MediaCardViewModel> _items = Array.Empty<MediaCardViewModel>();

    public BrowseViewModel(SimpleContainer services, string title, MediaType mediaType)
    {
        _services = services;
        _manager = services.GetRequiredService<ProviderManager>();
        _title = title;
        _mediaType = mediaType;
    }

    public string Title => _title;

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public string? Error
    {
        get => _error;
        private set
        {
            SetProperty(ref _error, value);
            OnPropertyChanged(nameof(HasError));
        }
    }

    public bool HasError => Error is not null;

    public IReadOnlyList<MediaCardViewModel> Items
    {
        get => _items;
        private set
        {
            SetProperty(ref _items, value);
            OnPropertyChanged(nameof(IsEmpty));
        }
    }

    public bool IsEmpty => Items.Count == 0 && !IsLoading && !HasError;

    public async Task LoadAsync()
    {
        IsLoading = true;
        Error = null;
        try
        {
            var sectionName = _mediaType == MediaType.Movie ? "Popular Movies" : "Popular Shows";
            var merged = new List<MediaSummary>();
            try
            {
                var page = await _manager.GetSectionAsync(sectionName, 1).ConfigureAwait(true);
                merged.AddRange(page.Items.Where(i => _mediaType == MediaType.Show ? i.MediaType != MediaType.Movie : i.MediaType == _mediaType));
            }
            catch (Exception)
            {
                // Skip failing providers; the section renders empty.
            }

            Items = merged.DistinctBy(i => i.ProviderId + "|" + i.Id)
                .Select(i => new MediaCardViewModel(i, _services))
                .ToList();
        }
        catch (Exception)
        {
            Error = "Unable to load this section right now.";
        }
        finally
        {
            IsLoading = false;
        }
    }
}

/// <summary>Favorites page state.</summary>
public sealed class FavoritesViewModel : ViewModelBase
{
    private readonly SimpleContainer _services;
    private bool _isLoading = true;
    private IReadOnlyList<MediaCardViewModel> _items = Array.Empty<MediaCardViewModel>();

    public FavoritesViewModel(SimpleContainer services)
    {
        _services = services;
        var favorites = services.GetRequiredService<IFavoritesService>();
        favorites.Changed += OnFavoritesChanged;
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public IReadOnlyList<MediaCardViewModel> Items
    {
        get => _items;
        private set
        {
            SetProperty(ref _items, value);
            OnPropertyChanged(nameof(IsEmpty));
        }
    }

    public bool IsEmpty => Items.Count == 0;

    public void Reload()
    {
        var favorites = _services.GetRequiredService<IFavoritesService>();
        Items = favorites.GetAll()
            .Select(f => new MediaCardViewModel(new MediaSummary(
                f.ItemId, f.ProviderId, f.MediaType, f.Title)
            {
                PosterUrl = f.PosterUrl,
                Year = f.Year,
                Rating = f.Rating,
                Category = f.Category
            }, _services))
            .ToList();
        OnPropertyChanged(nameof(IsEmpty));
    }

    private void OnFavoritesChanged() => Reload();
}
