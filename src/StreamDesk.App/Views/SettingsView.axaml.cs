using Avalonia.Controls;
using Avalonia.Interactivity;
using StreamDesk.App.ViewModels;
using StreamDesk.Core;

namespace StreamDesk.App.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }

    private SettingsViewModel? Vm => DataContext as SettingsViewModel;

    private void OnPickVlc(object? sender, RoutedEventArgs e) => _ = Vm?.PickVlcPathAsync(TopLevel.GetTopLevel(this) as Window ?? new Window());

    private void OnPickMpv(object? sender, RoutedEventArgs e) => _ = Vm?.PickMpvPathAsync(TopLevel.GetTopLevel(this) as Window ?? new Window());

    private void OnPickDownloadFolder(object? sender, RoutedEventArgs e) => _ = Vm?.PickDownloadDirectoryAsync(TopLevel.GetTopLevel(this) as Window ?? new Window());

    private void OnAddLibraryFolder(object? sender, RoutedEventArgs e) => _ = Vm?.AddLibraryFolderAsync(TopLevel.GetTopLevel(this) as Window ?? new Window());

    private void OnScanLibrary(object? sender, RoutedEventArgs e) => Vm?.ScanLibrary();

    private void OnImportPlaylist(object? sender, RoutedEventArgs e) => _ = Vm?.ImportPlaylistFileAsync(TopLevel.GetTopLevel(this) as Window ?? new Window());

    private void OnRemovePlaylist(object? sender, RoutedEventArgs e)
    {
        if (e.Source is Control { DataContext: PlaylistSource playlist } && Vm is not null)
        {
            Vm.RemovePlaylist(playlist);
        }
    }

    private void OnRemoveFolder(object? sender, RoutedEventArgs e)
    {
        if (e.Source is Control { DataContext: LibraryFolder folder } && Vm is not null)
        {
            Vm.RemoveLibraryFolder(folder);
        }
    }

    private void OnTestProvider(object? sender, RoutedEventArgs e)
    {
        if (e.Source is Control { DataContext: ProviderSettingViewModel provider })
        {
            _ = provider.TestConnectionAsync();
        }
    }

    private void OnTestAll(object? sender, RoutedEventArgs e) => _ = Vm?.TestProvidersAsync();

    private void OnSaveProviderSettings(object? sender, RoutedEventArgs e) => Vm?.SaveAll();

    private void OnResetSettings(object? sender, RoutedEventArgs e) => Vm?.ResetSettings();
}
