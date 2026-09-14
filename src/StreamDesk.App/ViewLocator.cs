using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using StreamDesk.App.ViewModels;

namespace StreamDesk.App;

/// <summary>
/// Maps view models to views by naming convention:
/// StreamDesk.App.ViewModels.XxxViewModel → StreamDesk.App.Views.XxxView.
/// </summary>
public sealed class ViewLocator : IDataTemplate
{
    public Control Build(object? data)
    {
        if (data is null)
        {
            return new TextBlock { Text = "No page" };
        }

        var name = data.GetType().FullName!.Replace("ViewModel", "View", StringComparison.Ordinal);
        var viewType = Type.GetType(name);
        if (viewType is null)
        {
            return new TextBlock { Text = "View not found: " + name };
        }

        return (Control)Activator.CreateInstance(viewType)!;
    }

    public bool Match(object? data) => data is ViewModelBase;
}
