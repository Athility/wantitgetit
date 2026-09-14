using System;
using Avalonia.Markup.Xaml;
using Avalonia.Controls.ApplicationLifetimes;
using StreamDesk.Application.Services;
using StreamDesk.App.Services;
using StreamDesk.App.ViewModels;

namespace StreamDesk.App;

public partial class App : global::Avalonia.Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var container = Composition.Build();
            Services = container;

            // Apply the persisted theme before the main window renders.
            var settings = container.GetService<ISettingsService>();
            if (settings is not null)
            {
                ThemeManager.Apply(settings.Current.Theme);
            }

            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainViewModel(container)
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    public static SimpleContainer Services { get; private set; } = null!;
}