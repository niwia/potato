using System.IO.Compression;
using Potato.Domain.ValueObjects;
using Potato.ManifestApi.Models;

namespace Potato.ManifestApi.Cache;

/// <summary>
/// File-system based implementation of IManifestCacheStore storing manifests in zip archives.
/// Matches the legacy directory structure and naming conventions.
/// </summary>
public sealed class FileManifestCacheStore : IManifestCacheStore
{
    private readonly string _cacheDirectory;

    public FileManifestCacheStore(string? cacheDirectory = null)
    {
        if (string.IsNullOrWhiteSpace(cacheDirectory))
        {
            string baseDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            _cacheDirectory = Path.Combine(baseDir, ".local", "share", "ACCELA", "hubcap_manifests");
        }
        else
        {
            _cacheDirectory = cacheDirectory;
        }

        if (!Directory.Exists(_cacheDirectory))
        {
            Directory.CreateDirectory(_cacheDirectory);
        }
    }

    public string GetZipFilePath(AppId appId, string branch)
    {
        string normalizedBranch = string.IsNullOrWhiteSpace(branch) ? "public" : branch.Trim();
        if (string.Equals(normalizedBranch, "public", StringComparison.OrdinalIgnoreCase))
        {
            return Path.Combine(_cacheDirectory, $"accela_fetch_{appId}.zip");
        }

        return Path.Combine(_cacheDirectory, $"accela_fetch_{appId}_branch_{normalizedBranch}.zip");
    }

    public async Task<IReadOnlyList<ManifestEntry>?> TryGetCachedManifestsAsync(
        AppId appId,
        string branch,
        IReadOnlyDictionary<DepotId, ManifestGid> requiredDepots,
        CancellationToken cancellationToken = default)
    {
        if (requiredDepots == null || requiredDepots.Count == 0)
        {
            return null;
        }

        string zipPath = GetZipFilePath(appId, branch);
        if (!File.Exists(zipPath))
        {
            return null;
        }

        try
        {
            byte[] zipBytes = await File.ReadAllBytesAsync(zipPath, cancellationToken);
            using var stream = new MemoryStream(zipBytes);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

            var archiveDepotMap = new Dictionary<DepotId, (ManifestGid Gid, ZipArchiveEntry Entry)>();

            foreach (var entry in archive.Entries)
            {
                if (!entry.FullName.EndsWith(".manifest", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string stem = Path.GetFileNameWithoutExtension(entry.FullName);
                var parts = stem.Split('_');
                if (parts.Length == 2 &&
                    DepotId.TryParse(parts[0], out var depotId) &&
                    ManifestGid.TryParse(parts[1], out var gid))
                {
                    archiveDepotMap[depotId] = (gid, entry);
                }
            }

            // Verify that EVERY required depot exists in the archive with the EXACT required GID
            foreach (var (reqDepotId, reqGid) in requiredDepots)
            {
                if (!archiveDepotMap.TryGetValue(reqDepotId, out var cached) || cached.Gid != reqGid)
                {
                    return null; // Cache miss or outdated GID
                }
            }

            // All required depots matched! Load their contents.
            var result = new List<ManifestEntry>(requiredDepots.Count);
            foreach (var (reqDepotId, reqGid) in requiredDepots)
            {
                var entry = archiveDepotMap[reqDepotId].Entry;
                await using var entryStream = entry.Open();
                using var ms = new MemoryStream((int)entry.Length);
                await entryStream.CopyToAsync(ms, cancellationToken);
                result.Add(new ManifestEntry(reqDepotId, reqGid, ms.ToArray()));
            }

            return result;
        }
        catch
        {
            return null;
        }
    }

    public async Task SaveManifestsAsync(
        AppId appId,
        string branch,
        IReadOnlyList<ManifestEntry> manifests,
        CancellationToken cancellationToken = default)
    {
        if (manifests == null || manifests.Count == 0)
        {
            return;
        }

        string zipPath = GetZipFilePath(appId, branch);
        string? dir = Path.GetDirectoryName(zipPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var manifest in manifests)
            {
                var entry = archive.CreateEntry(manifest.FileName, CompressionLevel.Optimal);
                await using var entryStream = entry.Open();
                await entryStream.WriteAsync(manifest.Content, cancellationToken);
            }
        }

        memoryStream.Seek(0, SeekOrigin.Begin);
        await File.WriteAllBytesAsync(zipPath, memoryStream.ToArray(), cancellationToken);
    }
}
