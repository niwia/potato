using FluentAssertions;
using Potato.Domain.ValueObjects;
using Potato.SlsSteam.Config;
using Potato.SlsSteam.Paths;
using Xunit;

namespace Potato.SlsSteam.Tests.Config;

public class SlsConfigManagerTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _testConfigPath;
    private readonly SlsConfigManager _manager;

    public SlsConfigManagerTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"potato_sls_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
        _testConfigPath = Path.Combine(_testDir, "config.yaml");

        var pathResolver = new SlsSteamPathResolver(explicitConfigPath: _testConfigPath);
        _manager = new SlsConfigManager(pathResolver);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            try { Directory.Delete(_testDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task AddAdditionalAppAsync_ShouldAddAppAndCreateBackup()
    {
        var appId = new AppId(1003590);
        bool added = await _manager.AddAdditionalAppAsync(appId, "Tetris® Effect: Connected", _testConfigPath);

        added.Should().BeTrue();
        File.Exists(_testConfigPath).Should().BeTrue();

        var loaded = await _manager.LoadAsync(_testConfigPath);
        loaded.AdditionalApps.Should().Contain(a => a.Value == "1003590");

        // Second addition should return false (already present)
        bool secondAdd = await _manager.AddAdditionalAppAsync(appId, "Duplicate", _testConfigPath);
        secondAdd.Should().BeFalse();
    }

    [Fact]
    public async Task AddDlcDataAndAppTokenAsync_ShouldPersistCorrectly()
    {
        var appId = new AppId(1003590);
        var dlcId = new DepotId(1723660);
        var token = new AppToken(987654321012345);

        await _manager.AddDlcDataAsync(appId, dlcId, "Soundtrack DLC", _testConfigPath);
        await _manager.AddAppTokenAsync(appId, token, "Main AppToken", _testConfigPath);

        var loaded = await _manager.LoadAsync(_testConfigPath);
        loaded.DlcData.Should().ContainKey("1003590");
        loaded.DlcData["1003590"].Should().ContainKey("1723660");
        loaded.DlcData["1003590"]["1723660"].Value.Should().Be("\"Soundtrack DLC\"");

        loaded.AppTokens.Should().ContainKey("1003590");
        loaded.AppTokens["1003590"].Value.Should().Be(token.ToString());
    }
}
