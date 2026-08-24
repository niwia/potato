using System;
using Avalonia;

namespace Potato.UI;

sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("=================================================");
        Console.WriteLine(" Potato - Steam Manifest Downloader UI");
        Console.WriteLine("=================================================");
        Console.ResetColor();

        try
        {
            Console.WriteLine("[UI] Initializing Avalonia application platform...");
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            Console.WriteLine("[UI] Application exited cleanly.");
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n[CRITICAL ERROR] Fatal unhandled exception in UI: {ex}");
            Console.ResetColor();
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
