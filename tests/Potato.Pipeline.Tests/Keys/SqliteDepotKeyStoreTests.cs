using FluentAssertions;
using Potato.Domain.ValueObjects;
using Potato.Pipeline.Keys;
using Xunit;

namespace Potato.Pipeline.Tests.Keys;

public class SqliteDepotKeyStoreTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteDepotKeyStore _store;

    public SqliteDepotKeyStoreTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"potato_keys_test_{Guid.NewGuid():N}.db");
        _store = new SqliteDepotKeyStore(_dbPath);
    }

    public void Dispose()
    {
        _store.Dispose();
        if (File.Exists(_dbPath))
        {
            try { File.Delete(_dbPath); } catch { }
        }
    }

    [Fact]
    public async Task SaveAndGet_ShouldRoundTripDepotKeysAndToken()
    {
        var appId = new AppId(1003590);
        var depot1 = new DepotId(1003591);
        var depot2 = new DepotId(1723660);
        var token = new AppToken(987654321012345);

        var keys = new Dictionary<DepotId, string>
        {
            [depot1] = "hex_key_1",
            [depot2] = "hex_key_2"
        };

        await _store.SaveDepotKeysAsync(appId, keys);
        await _store.SaveAppTokenAsync(appId, token);

        var loadedKeys = await _store.GetDepotKeysAsync(appId);
        var loadedToken = await _store.GetAppTokenAsync(appId);

        loadedKeys.Should().HaveCount(2);
        loadedKeys[depot1].Should().Be("hex_key_1");
        loadedKeys[depot2].Should().Be("hex_key_2");

        loadedToken.Should().NotBeNull();
        loadedToken!.Value.Should().Be(token);
    }
}
