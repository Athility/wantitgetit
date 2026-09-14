using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using StreamDesk.App.ViewModels;

namespace StreamDesk.App.Views;

public partial class LiveTvView : UserControl
{
    public LiveTvView()
    {
        InitializeComponent();
    }

    private LiveTvViewModel? Vm => DataContext as LiveTvViewModel;

    private MainViewModel? Main => (TopLevel.GetTopLevel(this) as Window)?.DataContext as MainViewModel;

    private void OnPlayChannel(object? sender, RoutedEventArgs e)
    {
        if (e.Source is Control { DataContext: ChannelItemViewModel channel })
        {
            Main?.OpenChannel(channel);
        }
    }

    private void OnToggleChannelFavorite(object? sender, RoutedEventArgs e)
    {
        if (e.Source is Control { DataContext: ChannelItemViewModel channel } && Vm is not null)
        {
            Vm.ToggleFavorite(channel);
        }
    }
}
