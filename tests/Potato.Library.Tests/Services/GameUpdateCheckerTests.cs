using FluentAssertions;
using Moq;
using Potato.Domain.Acf;
using Potato.Domain.ValueObjects;
using Potato.Library.Models;
using Potato.Library.Services;
using Potato.Pipeline.Keys;
using Potato.SteamMetadata.Models;
using Potato.SteamMetadata.Resolver;
using Xunit;

namespace Potato.Library.Tests.Services;

public class GameUpdateCheckerTests
{
    [Fact]
    public async Task CheckGameUpdateAsync_ShouldReturnUpToDate_WhenManifestGidsMatch()
    {
        var appId = new AppId(1003590);
        var depotId = new DepotId(1723660);
        var gid = new ManifestGid(111222333444555);

        var game = new InstalledGame
        {
            AppId = appId,
            Name = "Tetris Effect",
            BuildId = "100",
            InstalledDepots = new List<InstalledDepotInfo>
            {
                new(depotId, gid, 1024)
            }
        };

        var metadata = new SteamAppMetadata(
            appId,
            "Tetris Effect",
            "TetrisEffect",
            null,
            "100",
            null,
            new Dictionary<DepotId, SteamDepotInfo>
            {
                [depotId] = new(depotId, "Main Depot", size: "1024", manifestGid: gid, manifests: new Dictionary<string, ManifestGid> { ["public"] = gid })
            },
            source: "sqlite");

        var metaMock = new Mock<ISteamMetadataResolver>();
        metaMock.Setup(m => m.ResolveAppMetadataAsync(appId, It.IsAny<AppToken?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(metadata);

        var keyStoreMock = new Mock<IDepotKeyStore>();

        var checker = new GameUpdateChecker(metaMock.Object, keyStoreMock.Object);
        var result = await checker.CheckGameUpdateAsync(game);

        result.Status.Should().Be(UpdateStatus.UpToDate);
        game.UpdateStatus.Should().Be(UpdateStatus.UpToDate);
        game.PendingDepotUpdates.Should().BeEmpty();
    }

    [Fact]
    public async Task CheckGameUpdateAsync_ShouldReturnUpdateAvailable_WhenManifestGidChanges()
    {
        var appId = new AppId(1003590);
        var depotId = new DepotId(1723660);
        var oldGid = new ManifestGid(111222333444555);
        var newGid = new ManifestGid(999888777666555);

        var game = new InstalledGame
        {
            AppId = appId,
            Name = "Tetris Effect",
            BuildId = "100",
            InstalledDepots = new List<InstalledDepotInfo>
            {
                new(depotId, oldGid, 1024)
            }
        };

        var metadata = new SteamAppMetadata(
            appId,
            "Tetris Effect",
            "TetrisEffect",
            null,
            "101",
            null,
            new Dictionary<DepotId, SteamDepotInfo>
            {
                [depotId] = new(depotId, "Main Depot", size: "1024", manifestGid: newGid, manifests: new Dictionary<string, ManifestGid> { ["public"] = newGid })
            },
            source: "sqlite");

        var metaMock = new Mock<ISteamMetadataResolver>();
        metaMock.Setup(m => m.ResolveAppMetadataAsync(appId, It.IsAny<AppToken?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(metadata);

        var keyStoreMock = new Mock<IDepotKeyStore>();

        var checker = new GameUpdateChecker(metaMock.Object, keyStoreMock.Object);
        var result = await checker.CheckGameUpdateAsync(game);

        result.Status.Should().Be(UpdateStatus.UpdateAvailable);
        result.DepotDiffs.Should().HaveCount(1);
        result.DepotDiffs![0].InstalledGid.Should().Be(oldGid);
        result.DepotDiffs![0].TargetGid.Should().Be(newGid);
        game.UpdateStatus.Should().Be(UpdateStatus.UpdateAvailable);
    }
}
