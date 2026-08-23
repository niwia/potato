using FluentAssertions;
using Potato.Domain.ValueObjects;
using Potato.SteamMetadata.Models;
using Potato.SteamMetadata.Storage;
using Xunit;

namespace Potato.SteamMetadata.Tests.Storage;

public class SqliteSteamMetadataStoreTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteSteamMetadataStore _store;

    public SqliteSteamMetadataStoreTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"potato_test_meta_{Guid.NewGuid():N}.db");
        _store = new SqliteSteamMetadataStore(_dbPath);
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
    public async Task UpsertAndGet_ShouldRoundTripMetadataWithZstdCompression()
    {
        var appId = new AppId(1003590);
        var depotId = new DepotId(1003591);
        var manifestGid = new ManifestGid(8212840959240856401);

        var depots = new Dictionary<DepotId, SteamDepotInfo>
        {
            [depotId] = new SteamDepotInfo(
                depotId,
                name: "Tetris Effect Content",
                osList: "windows",
                language: "english",
                isSteamDeck: true,
                size: "524288000",
                manifestGid: manifestGid,
                manifests: new Dictionary<string, ManifestGid> { ["public"] = manifestGid })
        };

        var branches = new Dictionary<string, SteamBranchInfo>
        {
            ["public"] = new SteamBranchInfo("public", "1234567", "1680000000", pwdRequired: false)
        };

        var original = new SteamAppMetadata(
            appId,
            name: "Tetris Effect: Connected",
            installDir: "TetrisEffectConnected",
            headerUrl: "https://shared.akamai.steamstatic.com/store_item_assets/steam/apps/1003590/header.jpg",
            buildId: "1234567",
            timeUpdated: "1680000000",
            depots: depots,
            branches: branches,
            source: "steamcmd");

        await _store.UpsertAppInfoAsync(appId, original);

        var loaded = await _store.GetAppInfoAsync(appId);

        loaded.Should().NotBeNull();
        loaded!.AppId.Should().Be(appId);
        loaded.Name.Should().Be("Tetris Effect: Connected");
        loaded.InstallDir.Should().Be("TetrisEffectConnected");
        loaded.BuildId.Should().Be("1234567");
        loaded.HeaderUrl.Should().Be("https://shared.akamai.steamstatic.com/store_item_assets/steam/apps/1003590/header.jpg");
        loaded.Source.Should().Be("database");

        loaded.Depots.Should().HaveCount(1);
        loaded.Depots[depotId].Name.Should().Be("Tetris Effect Content");
        loaded.Depots[depotId].OsList.Should().Be("windows");
        loaded.Depots[depotId].Language.Should().Be("english");
        loaded.Depots[depotId].IsSteamDeck.Should().BeTrue();
        loaded.Depots[depotId].ManifestGid.Should().Be(manifestGid);

        loaded.Branches.Should().HaveCount(1);
        loaded.Branches["public"].BuildId.Should().Be("1234567");
    }

    [Fact]
    public async Task UpdateHeaderUrlAsync_ShouldOnlyUpdateHeaderPath()
    {
        var appId = new AppId(480);
        var original = new SteamAppMetadata(
            appId,
            name: "Spacewar",
            headerUrl: "https://old.url/header.jpg");

        await _store.UpsertAppInfoAsync(appId, original);

        bool updated = await _store.UpdateHeaderUrlAsync(appId, "https://new.url/apps/480/header.jpg");
        updated.Should().BeTrue();

        var loaded = await _store.GetAppInfoAsync(appId);
        loaded.Should().NotBeNull();
        loaded!.HeaderUrl.Should().Be("https://shared.akamai.steamstatic.com/store_item_assets/steam/apps/480/header.jpg");
        loaded.Name.Should().Be("Spacewar");
    }

    [Fact]
    public void NormalizeHeaderPath_And_ConstructFullUrl_ShouldBehaveIdenticallyToPython()
    {
        var appId = new AppId(1003590);
        string? norm1 = SqliteSteamMetadataStore.NormalizeHeaderPath(appId, "https://cdn.akamai.steamstatic.com/steam/apps/1003590/header.jpg?t=1658428800");
        norm1.Should().Be("1003590/header.jpg");

        string? url1 = SqliteSteamMetadataStore.ConstructFullUrl(norm1);
        url1.Should().Be("https://shared.akamai.steamstatic.com/store_item_assets/steam/apps/1003590/header.jpg");
    }
}
