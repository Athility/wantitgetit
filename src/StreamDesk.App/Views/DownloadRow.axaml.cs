using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using StreamDesk.App.ViewModels;

namespace StreamDesk.App.Views;

public partial class DownloadRow : UserControl
{
    public DownloadRow()
    {
        InitializeComponent();
    }

    private DownloadsViewModel? Vm => (TopLevel.GetTopLevel(this) as Window)?.DataContext is MainViewModel main
        ? main.Downloads
        : null;

    private void OnPause(object? sender, RoutedEventArgs e)
    {
        if (e.Source is Control { DataContext: DownloadItemViewModel item })
        {
            Vm?.Pause(item.Id);
        }
    }

    private void OnResume(object? sender, RoutedEventArgs e)
    {
        if (e.Source is Control { DataContext: DownloadItemViewModel item })
        {
            Vm?.Resume(item.Id);
        }
    }

    private void OnCancel(object? sender, RoutedEventArgs e)
    {
        if (e.Source is Control { DataContext: DownloadItemViewModel item })
        {
            Vm?.Cancel(item.Id);
        }
    }

    private void OnDelete(object? sender, RoutedEventArgs e)
    {
        if (e.Source is Control { DataContext: DownloadItemViewModel item })
        {
            Vm?.Delete(item.Id);
        }
    }
}
