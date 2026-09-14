using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using StreamDesk.Application.Managers;
using StreamDesk.Application.Providers;
using StreamDesk.Application.Services;
using StreamDesk.Core;
using StreamDesk.Infrastructure.Database;

namespace StreamDesk.App.ViewModels;

/// <summary>Live TV page state: channels, groups, search, favorites, refresh.</summary>
public sealed class LiveTvViewModel : ViewModelBase
{
    private readonly SimpleContainer _services;
    private readonly ProviderManager _manager;
    private bool _isLoading = true;
    private string? _error;
    private string _searchText = string.Empty;
    private string? _selectedGroup;
    private IReadOnlyList<ChannelItemViewModel> _channels = Array.Empty<ChannelItemViewModel>();
    private IReadOnlyList<ChannelItemViewModel> _visibleChannels = Array.Empty<ChannelItemViewModel>();
    private IReadOnlyList<string> _groups = Array.Empty<string>();
    private IReadOnlyList<PlaylistSource> _playlists = Array.Empty<PlaylistSource>();
    private bool _showFavoritesOnly;

    public LiveTvViewModel(SimpleContainer services)
    {
        _services = services;
        _manager = services.GetRequiredService<ProviderManager>();
    }

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

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                ApplyFilters();
            }
        }
    }

    public string? SelectedGroup
    {
        get => _selectedGroup;
        set
        {
            if (SetProperty(ref _selectedGroup, value))
            {
                ApplyFilters();
            }
        }
    }

    public bool ShowFavoritesOnly
    {
        get => _showFavoritesOnly;
        set
        {
            if (SetProperty(ref _showFavoritesOnly, value))
            {
                ApplyFilters();
            }
        }
    }

    public IReadOnlyList<ChannelItemViewModel> VisibleChannels
    {
        get => _visibleChannels;
        private set
        {
            SetProperty(ref _visibleChannels, value);
            OnPropertyChanged(nameof(HasNoChannels));
        }
    }

    public bool HasNoChannels => VisibleChannels.Count == 0 && !IsLoading;

    public IReadOnlyList<string> Groups
    {
        get => _groups;
        private set => SetProperty(ref _groups, value);
    }

    public IReadOnlyList<PlaylistSource> Playlists
    {
        get => _playlists;
        private set => SetProperty(ref _playlists, value);
    }

    public bool HasChannels => VisibleChannels.Count > 0;

    public PlaylistSource? SelectedPlaylist => Playlists.FirstOrDefault(p => p.Id == _services.GetRequiredService<ISettingsService>().Current.SelectedLiveTvPlaylistId);

    public async Task LoadAsync(bool forceRefresh = false)
    {
        IsLoading = true;
        Error = null;
        try
        {
            Playlists = _services.GetRequiredService<PlaylistsRepository>().GetAll();
            OnPropertyChanged(nameof(SelectedPlaylist));

            var favorites = _services.GetRequiredService<IFavoritesService>();
            var channels = await _manager.GetLiveChannelsAsync().ConfigureAwait(true);
            _channels = channels
                .Select(c => new ChannelItemViewModel(c, _services, favorites.IsFavorite(ProviderIds.M3U, c.Id)))
                .ToList();
            Groups = channels.Select(c => c.Group).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(g => g).ToList();
            ApplyFilters();

            if (forceRefresh)
            {
                await _manager.InitializeAllAsync().ConfigureAwait(true);
            }
        }
        catch (Exception)
        {
            Error = "Unable to load playlists. Check your Live TV settings.";
            _channels = Array.Empty<ChannelItemViewModel>();
            ApplyFilters();
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(HasChannels));
        }
    }

    public void ToggleFavorite(ChannelItemViewModel channel)
    {
        var favorites = _services.GetRequiredService<IFavoritesService>();
        favorites.ToggleChannel(channel.Channel);
        channel.IsFavorite = !channel.IsFavorite;
        if (ShowFavoritesOnly && !channel.IsFavorite)
        {
            ApplyFilters();
        }
    }

    private void ApplyFilters()
    {
        IEnumerable<ChannelItemViewModel> visible = _channels;
        if (ShowFavoritesOnly)
        {
            visible = visible.Where(c => c.IsFavorite);
        }

        if (!string.IsNullOrWhiteSpace(SelectedGroup) && SelectedGroup != "All")
        {
            visible = visible.Where(c => string.Equals(c.Group, SelectedGroup, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var term = SearchText.Trim();
            visible = visible.Where(c => c.Name.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        VisibleChannels = visible
            .OrderBy(c => c.Group, StringComparer.OrdinalIgnoreCase)
            .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        OnPropertyChanged(nameof(HasChannels));
    }
}
