using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;

using StreamDesk.Core;
using StreamDesk.Infrastructure.Storage;

namespace StreamDesk.App.ViewModels;

/// <summary>Row view model for one episode in the details page.</summary>
public sealed class EpisodeItemViewModel : ViewModelBase
{
    private readonly ImageCache? _imageCache;
    private Bitmap? _thumbnail;
    private bool _isDownloading;

    public EpisodeItemViewModel(EpisodeInfo episode, SimpleContainer services)
    {
        Episode = episode;
        Services = services;
        _imageCache = services.GetService<ImageCache>();
        _ = LoadThumbnailAsync();
    }

    public EpisodeInfo Episode { get; }

    public SimpleContainer Services { get; }

    public string Title => Episode.Title;

    public string Number => $"E{Episode.EpisodeNumber:00}";

    public string SeasonEpisode => $"S{Episode.SeasonNumber:00}E{Episode.EpisodeNumber:00}";

    public string Description => Episode.Description ?? string.Empty;

    public string Duration => Episode.DurationSeconds is { } seconds
        ? TimeSpan.FromSeconds(seconds).ToString(@"h\h\ m\m")
        : string.Empty;

    public Bitmap? Thumbnail
    {
        get => _thumbnail;
        private set
        {
            SetProperty(ref _thumbnail, value);
            OnPropertyChanged(nameof(HasThumbnail));
        }
    }

    public bool HasThumbnail => Thumbnail is not null;

    public bool IsDownloading
    {
        get => _isDownloading;
        set => SetProperty(ref _isDownloading, value);
    }

    private async Task LoadThumbnailAsync()
    {
        if (string.IsNullOrWhiteSpace(Episode.ThumbnailUrl))
        {
            return;
        }

        try
        {
            var cache = _imageCache;
            if (cache is null)
            {
                return;
            }

            var bytes = await cache.GetBytesAsync(Episode.ThumbnailUrl).ConfigureAwait(true);
            if (bytes is { Length: > 0 })
            {
                using var stream = new System.IO.MemoryStream(bytes);
                Thumbnail = new Bitmap(stream);
            }
        }
        catch (Exception)
        {
            // Thumbnails are optional decoration.
        }
    }
}

/// <summary>Row view model for one Live TV channel.</summary>
public sealed class ChannelItemViewModel : ViewModelBase
{
    private readonly ImageCache? _imageCache;
    private Bitmap? _logo;
    private bool _isFavorite;

    public ChannelItemViewModel(ChannelInfo channel, SimpleContainer services, bool isFavorite)
    {
        Channel = channel;
        Services = services;
        _isFavorite = isFavorite;
        _imageCache = services.GetService<ImageCache>();
        _ = LoadLogoAsync();
    }

    public ChannelInfo Channel { get; }

    public SimpleContainer Services { get; }

    public string Id => Channel.Id;

    public string Name => Channel.Name;

    public string Group => Channel.Group;

    public string StreamUrl => Channel.StreamUrl;

    public Bitmap? Logo
    {
        get => _logo;
        private set
        {
            SetProperty(ref _logo, value);
            OnPropertyChanged(nameof(HasLogo));
        }
    }

    public bool HasLogo => Logo is not null;

    public bool IsFavorite
    {
        get => _isFavorite;
        set => SetProperty(ref _isFavorite, value);
    }

    private async Task LoadLogoAsync()
    {
        if (string.IsNullOrWhiteSpace(Channel.LogoUrl))
        {
            return;
        }

        try
        {
            var cache = _imageCache;
            if (cache is null)
            {
                return;
            }

            var bytes = await cache.GetBytesAsync(Channel.LogoUrl).ConfigureAwait(true);
            if (bytes is { Length: > 0 })
            {
                using var stream = new System.IO.MemoryStream(bytes);
                Logo = new Bitmap(stream);
            }
        }
        catch (Exception)
        {
            // Logos are optional decoration.
        }
    }
}

/// <summary>A named row of media cards on the Home page.</summary>
public sealed class HomeSectionViewModel : ViewModelBase
{
    public HomeSectionViewModel(string title, IReadOnlyList<MediaCardViewModel> items)
    {
        Title = title;
        Items = items;
    }

    public string Title { get; }

    public IReadOnlyList<MediaCardViewModel> Items { get; }
}
