using Avalonia.Styling;
using StreamDesk.Core;

namespace StreamDesk.App.Services;

/// <summary>Applies the persisted theme mode to the Avalonia application.</summary>
public static class ThemeManager
{
    public static void Apply(ThemeMode mode)
    {
        var app = global::Avalonia.Application.Current;
        if (app is null)
        {
            return;
        }

        app.RequestedThemeVariant = mode switch
        {
            ThemeMode.Dark => ThemeVariant.Dark,
            ThemeMode.Light => ThemeVariant.Light,
            _ => ThemeVariant.Default
        };
    }
}
