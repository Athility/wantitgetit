using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using StreamDesk.App.ViewModels;

namespace StreamDesk.App.Views;

public partial class MediaCardView : UserControl
{
    public MediaCardView()
    {
        InitializeComponent();
        PointerPressed += OnPointerPressed;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is MediaCardViewModel card)
        {
            var window = TopLevel.GetTopLevel(this) as Window;
            if (window?.DataContext is MainViewModel main)
            {
                main.OpenDetails(card);
            }
        }
    }
}
