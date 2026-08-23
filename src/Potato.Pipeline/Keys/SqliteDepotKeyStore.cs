using System.Data;
using Microsoft.Data.Sqlite;
using Potato.Domain.ValueObjects;

namespace Potato.Pipeline.Keys;

/// <summary>
/// SQLite implementation of IDepotKeyStore managing depot_keys.db.
/// </summary>
public sealed class SqliteDepotKeyStore : IDepotKeyStore
{
    private readonly string _dbPath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private SqliteConnection? _connection;
    private bool _disposed;

    public SqliteDepotKeyStore(string? dbPath = null)
    {
        if (string.IsNullOrWhiteSpace(dbPath))
        {
            string baseDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            _dbPath = Path.Combine(baseDir, ".local", "share", "ACCELA", "db", "depot_keys.db");
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

        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        _connection = new SqliteConnection($"Data Source={_dbPath}");
        _connection.Open();

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS depot_keys (
                appid      TEXT NOT NULL,
                depot_id   TEXT NOT NULL,
                aes_key    TEXT NOT NULL,
                updated_at INTEGER NOT NULL DEFAULT 0,
                PRIMARY KEY (appid, depot_id)
            );
            CREATE TABLE IF NOT EXISTS app_tokens (
                appid      TEXT PRIMARY KEY,
                token      TEXT NOT NULL,
                updated_at INTEGER NOT NULL DEFAULT 0
            );";
        cmd.ExecuteNonQuery();
    }

    public async Task<IReadOnlyDictionary<DepotId, string>> GetDepotKeysAsync(
        AppId appId,
        CancellationToken cancellationToken = default)
    {
        if (!appId.IsValid) return new Dictionary<DepotId, string>();

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_connection == null || _connection.State != ConnectionState.Open)
            {
                return new Dictionary<DepotId, string>();
            }

            var result = new Dictionary<DepotId, string>();
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT depot_id, aes_key FROM depot_keys WHERE appid = @appid;";
            cmd.Parameters.AddWithValue("@appid", appId.ToString());

            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                string depotIdStr = reader.GetString(0);
                string aesKey = reader.GetString(1);

                if (DepotId.TryParse(depotIdStr, out var depotId) && depotId.Value != appId.Value)
                {
                    result[depotId] = aesKey;
                }
            }

            return result;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<AppToken?> GetAppTokenAsync(
        AppId appId,
        CancellationToken cancellationToken = default)
    {
        if (!appId.IsValid) return null;

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_connection == null || _connection.State != ConnectionState.Open) return null;

            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT token FROM app_tokens WHERE appid = @appid;";
            cmd.Parameters.AddWithValue("@appid", appId.ToString());

            var result = await cmd.ExecuteScalarAsync(cancellationToken);
            if (result != null && result != DBNull.Value && AppToken.TryParse(result.ToString(), out var token))
            {
                return token;
            }

            return null;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveDepotKeysAsync(
        AppId appId,
        IReadOnlyDictionary<DepotId, string> keys,
        CancellationToken cancellationToken = default)
    {
        if (!appId.IsValid || keys == null || keys.Count == 0) return;

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_connection == null || _connection.State != ConnectionState.Open) return;

            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            using var transaction = _connection.BeginTransaction();

            foreach (var (depotId, aesKey) in keys)
            {
                if (string.IsNullOrWhiteSpace(aesKey) || depotId.Value == appId.Value) continue;

                using var cmd = _connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = @"
                    INSERT OR REPLACE INTO depot_keys (appid, depot_id, aes_key, updated_at)
                    VALUES (@appid, @depot_id, @aes_key, @updated_at);";

                cmd.Parameters.AddWithValue("@appid", appId.ToString());
                cmd.Parameters.AddWithValue("@depot_id", depotId.ToString());
                cmd.Parameters.AddWithValue("@aes_key", aesKey.Trim());
                cmd.Parameters.AddWithValue("@updated_at", now);

                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveAppTokenAsync(
        AppId appId,
        AppToken token,
        CancellationToken cancellationToken = default)
    {
        if (!appId.IsValid || !token.IsValid) return;

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_connection == null || _connection.State != ConnectionState.Open) return;

            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
                INSERT OR REPLACE INTO app_tokens (appid, token, updated_at)
                VALUES (@appid, @token, @updated_at);";

            cmd.Parameters.AddWithValue("@appid", appId.ToString());
            cmd.Parameters.AddWithValue("@token", token.ToString());
            cmd.Parameters.AddWithValue("@updated_at", now);

            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _lock.Dispose();
        _connection?.Dispose();
    }
}
