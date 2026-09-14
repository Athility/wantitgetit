using System;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;

using StreamDesk.Core;
using StreamDesk.Infrastructure.Storage;

namespace StreamDesk.App.ViewModels;

/// <summary>View model for one poster card (grid/row item) with lazy poster load.</summary>
public sealed class MediaCardViewModel : ViewModelBase
{
    private readonly ImageCache? _imageCache;
    private Bitmap? _poster;
    private bool _isLoadingPoster;

    public MediaCardViewModel(MediaSummary summary, SimpleContainer services)
    {
        Summary = summary;
        Services = services;
        _imageCache = services.GetService<ImageCache>();
        _ = LoadPosterAsync();
    }

    public MediaSummary Summary { get; }

    public SimpleContainer Services { get; }

    public string Id => Summary.Id;

    public string ProviderId => Summary.ProviderId;

    public string Title => Summary.Title;

    public string YearLabel => Summary.Year?.ToString() ?? string.Empty;

    public string TypeLabel => Summary.MediaType switch
    {
        MediaType.Movie => "Movie",
        MediaType.Show => Summary.Category == "Anime" ? "Anime" : "Show",
        MediaType.Episode => "Episode",
        MediaType.Channel => "Live",
        _ => string.Empty
    };

    public string RatingLabel => Summary.Rating is null ? string.Empty : string.Format("{0:0.0}", Summary.Rating);

    public bool HasRating => Summary.Rating is not null;

    public string? Category => Summary.Category;

    public Bitmap? Poster
    {
        get => _poster;
        private set => SetProperty(ref _poster, value);
    }

    public bool IsLoadingPoster
    {
        get => _isLoadingPoster;
        private set => SetProperty(ref _isLoadingPoster, value);
    }

    private async Task LoadPosterAsync()
    {
        if (string.IsNullOrWhiteSpace(Summary.PosterUrl) || _imageCache is null)
        {
            return;
        }

        IsLoadingPoster = true;
        try
        {
            var bytes = await _imageCache.GetBytesAsync(Summary.PosterUrl).ConfigureAwait(true);
            if (bytes is { Length: > 0 })
            {
                using var stream = new System.IO.MemoryStream(bytes);
                Poster = new Bitmap(stream);
            }
        }
        catch (Exception)
        {
            // Leave poster null; the card shows a placeholder.
        }
        finally
        {
            IsLoadingPoster = false;
        }
    }
}
