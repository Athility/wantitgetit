using System;
using System.Globalization;
using Avalonia.Media;
using Avalonia.Data.Converters;

namespace StreamDesk.App.ViewModels;

/// <summary>Converters for the shell (nav labels, sidebar width).</summary>
public static class Converters
{
    public static readonly IValueConverter ExpandedToSidebarWidth = new FuncValueConverter<bool, double>(expanded => expanded ? 216 : 64);

    public static readonly IValueConverter NavHome = new FuncValueConverter<bool, string>(expanded => expanded ? "Home" : "\u2302");

    public static readonly IValueConverter NavMovies = new FuncValueConverter<bool, string>(expanded => expanded ? "Movies" : "\u25B6");

    public static readonly IValueConverter NavShows = new FuncValueConverter<bool, string>(expanded => expanded ? "TV Shows" : "\u25A6");

    public static readonly IValueConverter NavAnime = new FuncValueConverter<bool, string>(expanded => expanded ? "Anime" : "\u534D");

    public static readonly IValueConverter NavLive = new FuncValueConverter<bool, string>(expanded => expanded ? "Live TV" : "\u25CF");

    public static readonly IValueConverter NavDownloads = new FuncValueConverter<bool, string>(expanded => expanded ? "Downloads" : "\u2193");

    public static readonly IValueConverter NavFavorites = new FuncValueConverter<bool, string>(expanded => expanded ? "Favorites" : "\u2665");

    public static readonly IValueConverter NavSettings = new FuncValueConverter<bool, string>(expanded => expanded ? "Settings" : "\u2699");

    public static readonly IValueConverter CollapseLabel = new FuncValueConverter<bool, string>(expanded => expanded ? "\u2039 Collapse" : "\u203A");

    public static readonly IValueConverter FavoriteToBrush = new FuncValueConverter<bool, IBrush>(fav =>
        fav ? new SolidColorBrush(Color.FromRgb(229, 9, 20)) : new SolidColorBrush(Color.FromRgb(162, 167, 184)));
}
