using System.Runtime.InteropServices;
using SteamKit2;

namespace Potato.Core.Steam;

public class SteamPathResolver
{
    public static string? FindSteamInstall(string? customPath = null)
    {
        if (!string.IsNullOrEmpty(customPath) && Directory.Exists(customPath))
        {
            return customPath;
        }

        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            var searchPaths = new[]
            {
                // Flatpak
                Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", ".steam", "steam"),
                Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", ".local", "share", "Steam"),
                // Native
                Path.Combine(home, ".local", "share", "Steam"),
                Path.Combine(home, ".steam", "steam"),
                Path.Combine(home, ".steam", "root")
            };

            foreach (var path in searchPaths)
            {
                if (Directory.Exists(path) && (File.Exists(Path.Combine(path, "steam.sh")) || Directory.Exists(Path.Combine(path, "steamapps"))))
                {
                    return path;
                }
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var windowsPaths = new[]
            {
                @"C:\Program Files (x86)\Steam",
                @"C:\Program Files\Steam"
            };

            foreach (var path in windowsPaths)
            {
                if (Directory.Exists(path)) return path;
            }
        }

        return null;
    }

    public static List<string> GetSteamLibraries(string? steamInstallPath = null)
    {
        var libraries = new List<string>();
        var root = steamInstallPath ?? FindSteamInstall();

        if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
        {
            return libraries;
        }

        libraries.Add(root);

        var vdfPath = Path.Combine(root, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(vdfPath))
        {
            return libraries;
        }

        try
        {
            var kv = KeyValue.LoadAsText(vdfPath);
            if (kv != null)
            {
                foreach (var child in kv.Children)
                {
                    var pathVal = child["path"].Value;
                    if (!string.IsNullOrEmpty(pathVal) && Directory.Exists(pathVal) && !libraries.Contains(pathVal))
                    {
                        libraries.Add(pathVal);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error parsing libraryfolders.vdf: {ex.Message}");
        }

        return libraries;
    }
}
