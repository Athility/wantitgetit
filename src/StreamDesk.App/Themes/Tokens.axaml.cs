using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Avalonia.Controls;

namespace StreamDesk.App.Themes;

public class Tokens : ResourceDictionary
{
    public Tokens()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
