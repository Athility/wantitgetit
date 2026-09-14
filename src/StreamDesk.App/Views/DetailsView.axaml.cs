using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using StreamDesk.App.ViewModels;
using StreamDesk.Application.Managers;
using StreamDesk.Application.Services;
using StreamDesk.Core;

namespace StreamDesk.App.Views;

public partial class DetailsView : UserControl
{
    public DetailsView()
    {
        InitializeComponent();
    }

    private MainViewModel? Main => (TopLevel.GetTopLevel(this) as Window)?.DataContext as MainViewModel;

    private IPlaybackService Playback => App.Services.GetRequiredService<IPlaybackService>();

    private ProviderManager Manager => App.Services.GetRequiredService<ProviderManager>();

    private IWatchHistoryService History => App.Services.GetRequiredService<IWatchHistoryService>();

    private void OnPlay(object? sender, RoutedEventArgs e)
    {
        if (DataContext is DetailsViewModel { Details: { } details })
        {
            // Movies: resolve via the owning provider. Shows: play first episode of first season.
            if (details.MediaType == MediaType.Show && details.Seasons.Count > 0 && details.Seasons[0].Episodes.Count > 0)
            {
                PlayEpisode(details.Seasons[0].Episodes[0], details);
                return;
            }

            _ = PlayItemAsync(details.Id, details.ProviderId, details.Title);
        }
    }

    private void OnFavorite(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not DetailsViewModel { Details: { } details })
        {
            return;
        }

        var favorites = App.Services.GetRequiredService<IFavoritesService>();
        favorites.Toggle(new MediaSummary(details.Id, details.ProviderId, details.MediaType, details.Title)
        {
            PosterUrl = details.PosterUrl,
            Year = details.Year,
            Rating = details.Rating
        });
    }

    private void OnPlayEpisode(object? sender, RoutedEventArgs e)
    {
        if (e.Source is Control { DataContext: EpisodeItemViewModel episode } &&
            DataContext is DetailsViewModel { Details: { } details })
        {
            PlayEpisode(episode.Episode, details);
        }
    }

    private void OnDownloadEpisode(object? sender, RoutedEventArgs e)
    {
        if (e.Source is not Control { DataContext: EpisodeItemViewModel episode } ||
            DataContext is not DetailsViewModel { Details: { } details })
        {
            return;
        }

        var services = App.Services;
        var downloads = services.GetRequiredService<DownloadManager>();
        _ = Task.Run(async () =>
        {
            var resolved = await Manager.ResolveDownloadAsync(episode.Episode.Id, details.ProviderId).ConfigureAwait(false);
            if (resolved is null)
            {
                return; // Provider does not permit downloads; no fake feedback.
            }

            downloads.Enqueue(
                episode.Episode.Id,
                details.ProviderId,
                details.Title,
                $"S{episode.Episode.SeasonNumber:00} E{episode.Episode.EpisodeNumber:00} - {episode.Episode.Title}",
                resolved.Value.Source.Location,
                resolved.Value.SuggestedFileName,
                resolved.Value.Source.Kind == PlaybackSourceKind.NetworkStream ? null : null);
        });
    }

    private async void PlayEpisode(EpisodeInfo episode, MediaDetails series)
    {
        var source = await Manager.ResolvePlaybackAsync(episode.Id, series.ProviderId).ConfigureAwait(true);
        if (source is null)
        {
            return;
        }

        var services = App.Services;
        var subtitles = services.GetRequiredService<ISubtitleService>();
        var settings = services.GetRequiredService<ISettingsService>();
        if (source.Kind == PlaybackSourceKind.LocalFile)
        {
            source.Subtitles = subtitles.FindLocalSubtitleTracks(source.Location, settings.Current.PreferredSubtitleLanguage);
        }

        var history = services.GetRequiredService<IWatchHistoryService>();
        history.RecordProgress(new WatchHistoryEntry
        {
            ItemId = episode.Id,
            ProviderId = series.ProviderId,
            MediaType = MediaType.Episode,
            SeriesId = series.Id,
            Title = series.Title,
            Subtitle = $"S{episode.SeasonNumber:00} E{episode.EpisodeNumber:00} - {episode.Title}",
            PosterUrl = series.PosterUrl,
            SeasonNumber = episode.SeasonNumber,
            EpisodeNumber = episode.EpisodeNumber
        });

        var error = await Playback.PlayAsync(source).ConfigureAwait(true);
        if (error is not null)
        {
            ShowError(error);
        }
    }

    private async Task PlayItemAsync(string itemId, string providerId, string title)
    {
        var source = await Manager.ResolvePlaybackAsync(itemId, providerId).ConfigureAwait(true);
        if (source is null)
        {
            return;
        }

        var error = await Playback.PlayAsync(source).ConfigureAwait(true);
        if (error is not null)
        {
            ShowError(error);
        }
    }

    private void ShowError(string message)
    {
        _ = message; // Friendly playback errors surface through player dialogs later.
    }
}
