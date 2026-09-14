using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;

using StreamDesk.Core;
using StreamDesk.Infrastructure.Storage;

namespace StreamDesk.App.ViewModels;

/// <summary>Details page state for one movie, show or anime.</summary>
public sealed class DetailsViewModel : ViewModelBase
{
    private readonly SimpleContainer _services;
    private Bitmap? _backdrop;
    private Bitmap? _poster;

    public DetailsViewModel(SimpleContainer services)
    {
        _services = services;
    }

    public MediaDetails? Details { get; private set; }

    public string Title => Details?.Title ?? string.Empty;

    public string Tagline => Details?.Tagline ?? string.Empty;

    public string Description => Details?.Description ?? string.Empty;

    public string Year => Details?.Year?.ToString() ?? string.Empty;

    public string Rating => Details?.Rating is null ? string.Empty : string.Format("{0:0.0}", Details.Rating);

    public bool HasRating => Details?.Rating is not null;

    public string Runtime => Details?.RuntimeMinutes is { } minutes
        ? minutes >= 60 ? $"{minutes / 60}h {minutes % 60}m" : $"{minutes}m"
        : string.Empty;

    public string Genres => Details?.Genres.Count > 0 ? string.Join(", ", Details.Genres) : string.Empty;

    public string Cast => Details?.Cast.Count > 0
        ? string.Join(", ", Details.Cast.Select(c => c.Name))
        : string.Empty;

    public Bitmap? Backdrop
    {
        get => _backdrop;
        private set
        {
            SetProperty(ref _backdrop, value);
            OnPropertyChanged(nameof(HasBackdrop));
        }
    }

    public bool HasBackdrop => Backdrop is not null;

    public Bitmap? Poster
    {
        get => _poster;
        private set
        {
            SetProperty(ref _poster, value);
            OnPropertyChanged(nameof(HasPoster));
        }
    }

    public bool HasPoster => Poster is not null;

    public bool HasTagline => !string.IsNullOrEmpty(Tagline);

    public bool HasGenres => !string.IsNullOrEmpty(Genres);

    public bool HasCast => !string.IsNullOrEmpty(Cast);

    public string CastLabel => Cast.Length > 0 ? "Cast: " + Cast : string.Empty;

    public IReadOnlyList<SeasonViewModel> Seasons { get; private set; } = Array.Empty<SeasonViewModel>();

    public SeasonViewModel? SelectedSeason { get; private set; }

    public bool IsShow => Details?.MediaType == MediaType.Show;

    public bool IsMovie => Details?.MediaType == MediaType.Movie;

    public void Load(MediaDetails details)
    {
        Details = details;
        OnPropertyChanged(string.Empty);
        _ = LoadImagesAsync();

        Seasons = details.Seasons
            .Select(s => new SeasonViewModel(s, _services))
            .ToList();
        SelectedSeason = Seasons.FirstOrDefault();
        OnPropertyChanged(nameof(Seasons));
        OnPropertyChanged(nameof(SelectedSeason));
        OnPropertyChanged(nameof(IsShow));
        OnPropertyChanged(nameof(IsMovie));
    }

    public void SelectSeason(SeasonViewModel season)
    {
        SelectedSeason = season;
        OnPropertyChanged(nameof(SelectedSeason));
    }

    private async Task LoadImagesAsync()
    {
        var cache = _services.GetService<ImageCache>();
        if (cache is null || Details is null)
        {
            return;
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(Details.BackdropUrl))
            {
                var bytes = await cache.GetBytesAsync(Details.BackdropUrl).ConfigureAwait(true);
                if (bytes is { Length: > 0 })
                {
                    using var stream = new System.IO.MemoryStream(bytes);
                    Backdrop = new Bitmap(stream);
                }
            }

            if (!string.IsNullOrWhiteSpace(Details.PosterUrl))
            {
                var bytes = await cache.GetBytesAsync(Details.PosterUrl).ConfigureAwait(true);
                if (bytes is { Length: > 0 })
                {
                    using var stream = new System.IO.MemoryStream(bytes);
                    Poster = new Bitmap(stream);
                }
            }
        }
        catch (Exception)
        {
            // Images are decorative; text metadata still renders.
        }
    }
}

/// <summary>One season with its episode rows.</summary>
public sealed class SeasonViewModel : ViewModelBase
{
    public SeasonViewModel(SeasonInfo season, SimpleContainer services)
    {
        SeasonNumber = season.SeasonNumber;
        Name = season.Name;
        Episodes = season.Episodes
            .Select(e => new EpisodeItemViewModel(e, services))
            .ToList();
    }

    public int SeasonNumber { get; }

    public string Name { get; }

    public IReadOnlyList<EpisodeItemViewModel> Episodes { get; }
}
