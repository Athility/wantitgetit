using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Avalonia.Controls;

namespace StreamDesk.App.Themes;

public class LightDark : ResourceDictionary
{
    public LightDark()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
