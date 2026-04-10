using System;
using Avalonia;

namespace ObjectTracker.UI.Desktop;

/// <summary>
/// Provides the Avalonia application entry point and host configuration.
/// </summary>
public static class Program
{
    [STAThread]
    /// <summary>
    /// Starts the desktop application lifetime.
    /// </summary>
    /// <param name="args">The command-line arguments passed to the application.</param>
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    /// <summary>
    /// Builds the Avalonia application host with the project's shared configuration.
    /// </summary>
    /// <returns>The configured Avalonia application builder.</returns>
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
