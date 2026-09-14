using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using StreamDesk.App.ViewModels;

namespace StreamDesk.App.Views;

public partial class HelpDialog : UserControl
{
    public HelpDialog()
    {
        InitializeComponent();
    }

    private void OnClose(object? sender, RoutedEventArgs e)
    {
        if ((TopLevel.GetTopLevel(this) as Window)?.DataContext is MainViewModel main)
        {
            main.IsHelpOpen = false;
        }
    }
}
