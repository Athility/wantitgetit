using Avalonia.Markup.Xaml;
using Avalonia.Styling;

namespace StreamDesk.App.Themes;

public class Controls : Styles
{
    public Controls()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
