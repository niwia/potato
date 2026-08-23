namespace Potato.Core.Services;

public class ImageCacheService
{
    private readonly string _cacheDir;
    private readonly HttpClient _httpClient;

    public ImageCacheService(string? cacheDir = null, HttpClient? httpClient = null)
    {
        _cacheDir = cacheDir ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "Potato", "image_cache");
        Directory.CreateDirectory(_cacheDir);
        _httpClient = httpClient ?? new HttpClient();
    }

    public string GetLocalImagePath(uint appId)
    {
        return Path.Combine(_cacheDir, $"{appId}_header.jpg");
    }

    public async Task<string?> EnsureImageCachedAsync(uint appId, string? remoteUrl, CancellationToken ct = default)
    {
        var localPath = GetLocalImagePath(appId);
        if (File.Exists(localPath))
        {
            return localPath;
        }

        if (string.IsNullOrEmpty(remoteUrl))
        {
            return null;
        }

        try
        {
            var bytes = await _httpClient.GetByteArrayAsync(remoteUrl, ct);
            await File.WriteAllBytesAsync(localPath, bytes, ct);
            return localPath;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to cache image for App {appId}: {ex.Message}");
            return null;
        }
    }
}
