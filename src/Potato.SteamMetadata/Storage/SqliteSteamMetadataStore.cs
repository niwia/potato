using System.Data;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using Potato.Domain.ValueObjects;
using Potato.SteamMetadata.Models;
using ZstdSharp;

namespace Potato.SteamMetadata.Storage;

/// <summary>
/// SQLite implementation of ISteamMetadataStore matching the legacy schema and Zstandard compressed JSON format.
/// </summary>
public sealed class SqliteSteamMetadataStore : ISteamMetadataStore
{
    public const long ExpirationSeconds = 1_209_600; // 14 days
    private const string AkamaiBaseUrl = "https://shared.akamai.steamstatic.com/store_item_assets/steam/apps/";

    private readonly string _dbPath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly Compressor _compressor;
    private readonly Decompressor _decompressor;
    private SqliteConnection? _connection;
    private bool _disposed;

    public SqliteSteamMetadataStore(string? dbPath = null)
    {
        if (string.IsNullOrWhiteSpace(dbPath))
        {
            string baseDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            _dbPath = Path.Combine(baseDir, ".local", "share", "ACCELA", "steam_headers.db");
        }
        else
        {
            _dbPath = dbPath;
        }

        string? dir = Path.GetDirectoryName(_dbPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        _compressor = new Compressor(3);
        _decompressor = new Decompressor();

        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        _connection = new SqliteConnection($"Data Source={_dbPath}");
        _connection.Open();

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS apps (
                appid INTEGER PRIMARY KEY,
                name TEXT,
                header_path TEXT,
                installdir TEXT,
                depots_json BLOB,
                last_updated INTEGER
            );";
        cmd.ExecuteNonQuery();
    }

    public async Task<SteamAppMetadata?> GetAppInfoAsync(
        AppId appId,
        bool bypassExpiration = false,
        CancellationToken cancellationToken = default)
    {
        if (!appId.IsValid) return null;

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_connection == null || _connection.State != ConnectionState.Open)
            {
                return null;
            }

            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT name, header_path, installdir, depots_json, last_updated FROM apps WHERE appid = @appid;";
            cmd.Parameters.AddWithValue("@appid", appId.Value);

            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            long lastUpdated = reader.IsDBNull(4) ? 0 : reader.GetInt64(4);
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            if (!bypassExpiration && (now - lastUpdated) > ExpirationSeconds)
            {
                return null; // Expired
            }

            string? name = reader.IsDBNull(0) ? null : reader.GetString(0);
            string? headerPath = reader.IsDBNull(1) ? null : reader.GetString(1);
            string? installDir = reader.IsDBNull(2) ? null : reader.GetString(2);
            byte[]? depotsBlob = reader.IsDBNull(3) ? null : (byte[])reader.GetValue(3);

            string? headerUrl = ConstructFullUrl(headerPath);

            var (depots, branches, buildId, timeUpdated) = DecompressAndParseDepots(depotsBlob, appId);

            return new SteamAppMetadata(
                appId,
                name,
                installDir,
                headerUrl,
                buildId,
                timeUpdated,
                depots,
                branches,
                source: "database");
        }
        catch
        {
            return null;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task UpsertAppInfoAsync(
        AppId appId,
        SteamAppMetadata metadata,
        CancellationToken cancellationToken = default)
    {
        if (!appId.IsValid || metadata == null) return;

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_connection == null || _connection.State != ConnectionState.Open)
            {
                return;
            }

            string? headerPath = NormalizeHeaderPath(appId, metadata.HeaderUrl);
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            byte[] compressedDepots = CompressDepots(metadata);

            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
                INSERT OR REPLACE INTO apps
                (appid, name, header_path, installdir, depots_json, last_updated)
                VALUES (@appid, @name, @header_path, @installdir, @depots_json, @last_updated);";

            cmd.Parameters.AddWithValue("@appid", appId.Value);
            cmd.Parameters.AddWithValue("@name", (object?)metadata.Name ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@header_path", (object?)headerPath ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@installdir", (object?)metadata.InstallDir ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@depots_json", compressedDepots);
            cmd.Parameters.AddWithValue("@last_updated", now);

            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> UpdateHeaderUrlAsync(
        AppId appId,
        string headerUrl,
        CancellationToken cancellationToken = default)
    {
        if (!appId.IsValid || string.IsNullOrWhiteSpace(headerUrl)) return false;

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_connection == null || _connection.State != ConnectionState.Open)
            {
                return false;
            }

            string? headerPath = NormalizeHeaderPath(appId, headerUrl);
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            using var checkCmd = _connection.CreateCommand();
            checkCmd.CommandText = "SELECT appid FROM apps WHERE appid = @appid;";
            checkCmd.Parameters.AddWithValue("@appid", appId.Value);
            var exists = await checkCmd.ExecuteScalarAsync(cancellationToken);
            if (exists == null) return false;

            using var updateCmd = _connection.CreateCommand();
            updateCmd.CommandText = "UPDATE apps SET header_path = @header_path, last_updated = @last_updated WHERE appid = @appid;";
            updateCmd.Parameters.AddWithValue("@header_path", (object?)headerPath ?? DBNull.Value);
            updateCmd.Parameters.AddWithValue("@last_updated", now);
            updateCmd.Parameters.AddWithValue("@appid", appId.Value);

            int rows = await updateCmd.ExecuteNonQueryAsync(cancellationToken);
            return rows > 0;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task DeleteAppInfoAsync(
        AppId appId,
        CancellationToken cancellationToken = default)
    {
        if (!appId.IsValid) return;

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_connection == null || _connection.State != ConnectionState.Open) return;

            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "DELETE FROM apps WHERE appid = @appid;";
            cmd.Parameters.AddWithValue("@appid", appId.Value);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<long?> GetCacheTimeAsync(
        AppId appId,
        CancellationToken cancellationToken = default)
    {
        if (!appId.IsValid) return null;

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_connection == null || _connection.State != ConnectionState.Open) return null;

            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT last_updated FROM apps WHERE appid = @appid;";
            cmd.Parameters.AddWithValue("@appid", appId.Value);
            var result = await cmd.ExecuteScalarAsync(cancellationToken);
            if (result != null && result != DBNull.Value)
            {
                return Convert.ToInt64(result);
            }
            return null;
        }
        finally
        {
            _lock.Release();
        }
    }

    private byte[] CompressDepots(SteamAppMetadata metadata)
    {
        var root = new JsonObject();

        foreach (var (depotId, depotInfo) in metadata.Depots)
        {
            var dNode = new JsonObject
            {
                ["name"] = depotInfo.Name,
                ["oslist"] = depotInfo.OsList,
                ["language"] = depotInfo.Language,
                ["steamdeck"] = depotInfo.IsSteamDeck,
                ["size"] = depotInfo.Size,
                ["manifest_id"] = depotInfo.ManifestGid?.Value.ToString()
            };

            if (depotInfo.Manifests != null && depotInfo.Manifests.Count > 0)
            {
                var mNode = new JsonObject();
                foreach (var (bName, gid) in depotInfo.Manifests)
                {
                    mNode[bName] = gid.Value.ToString();
                }
                dNode["manifests"] = mNode;
            }

            root[depotId.ToString()] = dNode;
        }

        if (metadata.Branches != null && metadata.Branches.Count > 0)
        {
            var bRoot = new JsonObject();
            foreach (var (bName, bInfo) in metadata.Branches)
            {
                bRoot[bName] = new JsonObject
                {
                    ["buildid"] = bInfo.BuildId,
                    ["timeupdated"] = bInfo.TimeUpdated,
                    ["pwdrequired"] = bInfo.PwdRequired ? "1" : "0"
                };
            }
            root["branches"] = bRoot;
        }
        else if (!string.IsNullOrEmpty(metadata.BuildId))
        {
            root["branches"] = new JsonObject
            {
                ["public"] = new JsonObject
                {
                    ["buildid"] = metadata.BuildId,
                    ["timeupdated"] = metadata.TimeUpdated
                }
            };
        }

        byte[] jsonBytes = JsonSerializer.SerializeToUtf8Bytes(root);
        return _compressor.Wrap(jsonBytes).ToArray();
    }

    private (Dictionary<DepotId, SteamDepotInfo> Depots, Dictionary<string, SteamBranchInfo> Branches, string? BuildId, string? TimeUpdated)
        DecompressAndParseDepots(byte[]? blob, AppId appId)
    {
        var depots = new Dictionary<DepotId, SteamDepotInfo>();
        var branches = new Dictionary<string, SteamBranchInfo>();
        string? buildId = null;
        string? timeUpdated = null;

        if (blob == null || blob.Length == 0)
        {
            return (depots, branches, buildId, timeUpdated);
        }

        try
        {
            var decompressedBytes = _decompressor.Unwrap(blob);
            var jsonNode = JsonNode.Parse(decompressedBytes);
            if (jsonNode is not JsonObject root)
            {
                return (depots, branches, buildId, timeUpdated);
            }

            // Extract branches
            if (root.TryGetPropertyValue("branches", out var branchesNode) && branchesNode is JsonObject bObj)
            {
                foreach (var (bName, bVal) in bObj)
                {
                    if (bVal is JsonObject bInfoObj)
                    {
                        string? bBuildId = bInfoObj["buildid"]?.ToString();
                        string? bTimeUpdated = bInfoObj["timeupdated"]?.ToString();
                        bool pwdReq = bInfoObj["pwdrequired"]?.ToString() == "1";
                        branches[bName] = new SteamBranchInfo(bName, bBuildId, bTimeUpdated, pwdReq);

                        if (string.Equals(bName, "public", StringComparison.OrdinalIgnoreCase))
                        {
                            buildId = bBuildId;
                            timeUpdated = bTimeUpdated;
                        }
                    }
                }
            }

            // Extract depots
            foreach (var (key, val) in root)
            {
                if (string.Equals(key, "branches", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(key, "workshopdepots", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(key, "branches_public", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!DepotId.TryParse(key, out var depotId) || val is not JsonObject dObj)
                {
                    continue;
                }

                string? name = dObj["name"]?.ToString();
                string? osList = dObj["oslist"]?.ToString();
                string? language = dObj["language"]?.ToString();
                bool isSteamDeck = dObj["steamdeck"]?.GetValue<bool>() ?? false;
                string? size = dObj["size"]?.ToString();

                ManifestGid? manifestGid = null;
                var manifestIdVal = dObj["manifest_id"]?.ToString();
                if (ManifestGid.TryParse(manifestIdVal, out var parsedGid))
                {
                    manifestGid = parsedGid;
                }

                var manifestsMap = new Dictionary<string, ManifestGid>();
                if (dObj.TryGetPropertyValue("manifests", out var mNode) && mNode is JsonObject mObj)
                {
                    foreach (var (mBranch, mVal) in mObj)
                    {
                        string? gidStr = null;
                        if (mVal is JsonObject mDict)
                        {
                            gidStr = mDict["gid"]?.ToString();
                        }
                        else if (mVal != null)
                        {
                            gidStr = mVal.ToString();
                        }

                        if (ManifestGid.TryParse(gidStr, out var mGid))
                        {
                            manifestsMap[mBranch] = mGid;
                            if (manifestGid == null && string.Equals(mBranch, "public", StringComparison.OrdinalIgnoreCase))
                            {
                                manifestGid = mGid;
                            }
                        }
                    }
                }

                depots[depotId] = new SteamDepotInfo(
                    depotId,
                    name,
                    osList,
                    language,
                    isSteamDeck,
                    size,
                    manifestGid,
                    manifestsMap);
            }
        }
        catch
        {
            // Ignore decompression / json parsing errors
        }

        return (depots, branches, buildId, timeUpdated);
    }

    public static string? NormalizeHeaderPath(AppId appId, string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;

        string cleanUrl = url.Split('?')[0];
        if (cleanUrl.Contains("/apps/"))
        {
            var parts = cleanUrl.Split(new[] { "/apps/" }, StringSplitOptions.None);
            if (parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1]))
            {
                return parts[1];
            }
        }

        return $"{appId}/header.jpg";
    }

    public static string? ConstructFullUrl(string? headerPath)
    {
        if (string.IsNullOrWhiteSpace(headerPath)) return null;
        if (headerPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            headerPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return headerPath;
        }

        return $"{AkamaiBaseUrl}{headerPath.TrimStart('/')}";
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _lock.Dispose();
        _compressor.Dispose();
        _decompressor.Dispose();
        _connection?.Dispose();
    }
}
