using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using StreamDesk.App.ViewModels;

namespace StreamDesk.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.K when e.KeyModifiers.HasFlag(KeyModifiers.Control):
            case Key.F when e.KeyModifiers.HasFlag(KeyModifiers.Control):
                FocusSearch();
                e.Handled = true;
                break;
            case Key.L when e.KeyModifiers.HasFlag(KeyModifiers.Control):
                vm.NavigateToLiveTv();
                e.Handled = true;
                break;
            case Key.D when e.KeyModifiers.HasFlag(KeyModifiers.Control):
                vm.NavigateToDownloads();
                e.Handled = true;
                break;
            case Key.OemComma when e.KeyModifiers.HasFlag(KeyModifiers.Control):
                vm.NavigateToSettings();
                e.Handled = true;
                break;
            case Key.F1:
                vm.IsHelpOpen = !vm.IsHelpOpen;
                e.Handled = true;
                break;
            case Key.Escape:
                if (vm.IsHelpOpen)
                {
                    vm.IsHelpOpen = false;
                    e.Handled = true;
                }
                else if (vm.IsDetailsOpen)
                {
                    vm.CloseDetails();
                    e.Handled = true;
                }
                else if (vm.CurrentPage == AppPage.Search)
                {
                    vm.NavigateHome();
                    e.Handled = true;
                }
                break;
        }
    }

    private void FocusSearch()
    {
        var searchBox = this.FindControl<TextBox>("SearchBox");
        searchBox?.Focus();
    }
}