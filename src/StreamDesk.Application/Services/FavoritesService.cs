using System;
using System.Collections.Generic;
using System.Linq;
using StreamDesk.Application.Providers;
using StreamDesk.Core;
using StreamDesk.Infrastructure.Database;

namespace StreamDesk.Application.Services;

/// <summary>Favorites service over the SQLite favorites store.</summary>
public sealed class FavoritesService : IFavoritesService
{
    private readonly FavoritesRepository _repository;

    public FavoritesService(FavoritesRepository repository)
    {
        _repository = repository;
    }

    public event Action? Changed;

    public IReadOnlyList<FavoriteItem> GetAll() => _repository.GetAll();

    public bool IsFavorite(string providerId, string itemId) => _repository.IsFavorite(providerId, itemId);

    public void Toggle(MediaSummary summary)
    {
        if (_repository.IsFavorite(summary.ProviderId, summary.Id))
        {
            _repository.Remove(summary.ProviderId, summary.Id);
        }
        else
        {
            _repository.Add(new FavoriteItem
            {
                ItemId = summary.Id,
                ProviderId = summary.ProviderId,
                MediaType = summary.MediaType,
                Title = summary.Title,
                PosterUrl = summary.PosterUrl,
                Year = summary.Year,
                Rating = summary.Rating,
                Category = summary.Category
            });
        }

        Changed?.Invoke();
    }

    public void ToggleChannel(ChannelInfo channel)
    {
        if (_repository.IsFavorite(ProviderIds.M3U, channel.Id))
        {
            _repository.Remove(ProviderIds.M3U, channel.Id);
        }
        else
        {
            _repository.Add(new FavoriteItem
            {
                ItemId = channel.Id,
                ProviderId = ProviderIds.M3U,
                MediaType = MediaType.Channel,
                Title = channel.Name,
                PosterUrl = channel.LogoUrl,
                Category = channel.Group
            });
        }

        Changed?.Invoke();
    }

    public void Remove(string providerId, string itemId) => _repository.Remove(providerId, itemId);
}
