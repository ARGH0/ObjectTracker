using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace ObjectTracker.UI.Desktop;

/// <summary>
/// Represents the Avalonia application root for the desktop UI.
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// Loads the application-level XAML resources.
    /// </summary>
    public override void Initialize()
    {
        // Load the application-level styles and resources declared in App.axaml.
        AvaloniaXamlLoader.Load(this);
    }

    /// <summary>
    /// Creates the main window after the Avalonia framework has finished initializing.
    /// </summary>
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // The project currently uses a single main window as the desktop shell.
            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}