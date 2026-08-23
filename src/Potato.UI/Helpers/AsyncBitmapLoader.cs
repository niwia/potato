using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;

namespace Potato.UI.Helpers;

public static class AsyncBitmapLoader
{
    private static readonly ConcurrentDictionary<string, Bitmap> Cache = new();
    private static readonly HttpClient HttpClient = new();

    public static async Task<Bitmap?> LoadFromUrlAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;

        if (Cache.TryGetValue(url, out var cached))
        {
            return cached;
        }

        try
        {
            var bytes = await HttpClient.GetByteArrayAsync(url);
            using var stream = new MemoryStream(bytes);
            var bitmap = new Bitmap(stream);
            Cache[url] = bitmap;
            return bitmap;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load bitmap from {url}: {ex.Message}");
            return null;
        }
    }
}
