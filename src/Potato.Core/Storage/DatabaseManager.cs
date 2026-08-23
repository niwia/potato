using Microsoft.Data.Sqlite;

namespace Potato.Core.Storage;

public class DatabaseManager
{
    private readonly string _dbDirectory;
    private readonly string _headersDbPath;
    private readonly string _depotKeysDbPath;

    public DatabaseManager(string? baseDir = null)
    {
        _dbDirectory = baseDir ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "Potato", "db");
        Directory.CreateDirectory(_dbDirectory);

        _headersDbPath = Path.Combine(_dbDirectory, "steam_headers.db");
        _depotKeysDbPath = Path.Combine(_dbDirectory, "depot_keys.db");

        InitializeDatabases();
    }

    private void InitializeDatabases()
    {
        // Headers DB
        using (var conn = new SqliteConnection($"Data Source={_headersDbPath}"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                PRAGMA journal_mode=WAL;
                CREATE TABLE IF NOT EXISTS headers (
                    app_id INTEGER PRIMARY KEY,
                    name TEXT NOT NULL,
                    header_url TEXT,
                    updated_at INTEGER NOT NULL
                );
            ";
            cmd.ExecuteNonQuery();
        }

        // Depot Keys DB
        using (var conn = new SqliteConnection($"Data Source={_depotKeysDbPath}"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                PRAGMA journal_mode=WAL;
                CREATE TABLE IF NOT EXISTS depot_keys (
                    depot_id INTEGER PRIMARY KEY,
                    depot_key TEXT NOT NULL,
                    updated_at INTEGER NOT NULL
                );
            ";
            cmd.ExecuteNonQuery();
        }
    }

    public async Task CacheHeaderAsync(uint appId, string name, string? headerUrl)
    {
        using var conn = new SqliteConnection($"Data Source={_headersDbPath}");
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO headers (app_id, name, header_url, updated_at)
            VALUES (@appId, @name, @headerUrl, @updatedAt)
            ON CONFLICT(app_id) DO UPDATE SET
                name=excluded.name,
                header_url=excluded.header_url,
                updated_at=excluded.updated_at;
        ";
        cmd.Parameters.AddWithValue("@appId", appId);
        cmd.Parameters.AddWithValue("@name", name);
        cmd.Parameters.AddWithValue("@headerUrl", (object?)headerUrl ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@updatedAt", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<(string? Name, string? HeaderUrl)> GetHeaderAsync(uint appId)
    {
        using var conn = new SqliteConnection($"Data Source={_headersDbPath}");
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name, header_url FROM headers WHERE app_id = @appId";
        cmd.Parameters.AddWithValue("@appId", appId);
        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            var name = reader.GetString(0);
            var headerUrl = reader.IsDBNull(1) ? null : reader.GetString(1);
            return (name, headerUrl);
        }
        return (null, null);
    }

    public async Task StoreDepotKeyAsync(uint depotId, string depotKey)
    {
        using var conn = new SqliteConnection($"Data Source={_depotKeysDbPath}");
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO depot_keys (depot_id, depot_key, updated_at)
            VALUES (@depotId, @depotKey, @updatedAt)
            ON CONFLICT(depot_id) DO UPDATE SET
                depot_key=excluded.depot_key,
                updated_at=excluded.updated_at;
        ";
        cmd.Parameters.AddWithValue("@depotId", depotId);
        cmd.Parameters.AddWithValue("@depotKey", depotKey);
        cmd.Parameters.AddWithValue("@updatedAt", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<string?> GetDepotKeyAsync(uint depotId)
    {
        using var conn = new SqliteConnection($"Data Source={_depotKeysDbPath}");
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT depot_key FROM depot_keys WHERE depot_id = @depotId";
        cmd.Parameters.AddWithValue("@depotId", depotId);
        var result = await cmd.ExecuteScalarAsync();
        return result as string;
    }
}
