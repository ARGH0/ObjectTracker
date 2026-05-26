using Avalonia;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace ObjectTracker.UI.Desktop;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
        {
            LogUnhandledException("AppDomain.CurrentDomain.UnhandledException", eventArgs.ExceptionObject as Exception);
        };

        TaskScheduler.UnobservedTaskException += (_, eventArgs) =>
        {
            LogUnhandledException("TaskScheduler.UnobservedTaskException", eventArgs.Exception);
            eventArgs.SetObserved();
        };

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            LogUnhandledException("Program.Main", ex);
            throw;
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .With(new X11PlatformOptions { RenderingMode = new[] { X11RenderingMode.Software } })
            .LogToTrace();

    private static void LogUnhandledException(string source, Exception? exception)
    {
        var details = exception is null
            ? $"{source}: non-exception unhandled error"
            : $"{source}: {exception}";

        Debug.WriteLine(details);
        Console.Error.WriteLine(details);
    }
}
