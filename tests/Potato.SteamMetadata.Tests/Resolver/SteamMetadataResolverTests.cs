using FluentAssertions;
using Moq;
using Potato.Domain.ValueObjects;
using Potato.SteamMetadata.Clients;
using Potato.SteamMetadata.Models;
using Potato.SteamMetadata.Resolver;
using Potato.SteamMetadata.Storage;
using Xunit;

namespace Potato.SteamMetadata.Tests.Resolver;

public class SteamMetadataResolverTests
{
    private readonly Mock<ISteamMetadataStore> _storeMock = new();
    private readonly Mock<ISteamCmdRestClient> _steamCmdMock = new();
    private readonly Mock<ISteamPicsClient> _picsMock = new();
    private readonly Mock<ISteamStoreWebClient> _storeWebMock = new();

    private readonly SteamMetadataResolver _resolver;

    public SteamMetadataResolverTests()
    {
        _resolver = new SteamMetadataResolver(
            _storeMock.Object,
            _steamCmdMock.Object,
            _picsMock.Object,
            _storeWebMock.Object);
    }

    [Fact]
    public async Task ResolveAppMetadataAsync_ShouldReturnFromDbCache_WhenCacheHasDepotsAndValidName()
    {
        var appId = new AppId(746850);
        var cachedMetadata = new SteamAppMetadata(
            appId,
            name: "Cloudpunk",
            depots: new Dictionary<DepotId, SteamDepotInfo>
            {
                [new DepotId(746851)] = new SteamDepotInfo(new DepotId(746851))
            },
            source: "database");

        _storeMock.Setup(s => s.GetAppInfoAsync(appId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedMetadata);

        var result = await _resolver.ResolveAppMetadataAsync(appId);

        result.Should().NotBeNull();
        result!.Source.Should().Be("database");
        result.Name.Should().Be("Cloudpunk");

        // Verified network was not touched
        _steamCmdMock.Verify(c => c.FetchAppInfoAsync(It.IsAny<AppId>(), It.IsAny<CancellationToken>()), Times.Never);
        _picsMock.Verify(c => c.FetchProductInfoAsync(It.IsAny<AppId>(), It.IsAny<AppToken?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResolveAppMetadataAsync_ShouldForceLiveFetch_WhenCachedNameIsGeneric()
    {
        var appId = new AppId(746850);
        var cachedGeneric = new SteamAppMetadata(
            appId,
            name: "App 746850",
            depots: new Dictionary<DepotId, SteamDepotInfo>
            {
                [new DepotId(746851)] = new SteamDepotInfo(new DepotId(746851))
            },
            source: "database");

        var liveData = new SteamAppMetadata(
            appId,
            name: "Cloudpunk",
            depots: new Dictionary<DepotId, SteamDepotInfo>
            {
                [new DepotId(746851)] = new SteamDepotInfo(new DepotId(746851))
            },
            source: "steamcmd");

        _storeMock.Setup(s => s.GetAppInfoAsync(appId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedGeneric);

        _steamCmdMock.Setup(c => c.FetchAppInfoAsync(appId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(liveData);

        _storeWebMock.Setup(c => c.FetchStoreDetailsAsync(appId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SteamStoreDetails("Cloudpunk", "https://store.url/header.jpg", "Cloudpunk", null));

        var result = await _resolver.ResolveAppMetadataAsync(appId);

        result.Should().NotBeNull();
        result!.Name.Should().Be("Cloudpunk");
        result.Source.Should().Be("steamcmd");

        // Verified live fetch was triggered and DB updated
        _steamCmdMock.Verify(c => c.FetchAppInfoAsync(appId, It.IsAny<CancellationToken>()), Times.Once);
        _storeMock.Verify(s => s.UpsertAppInfoAsync(appId, It.IsAny<SteamAppMetadata>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResolveAppMetadataAsync_ShouldFallbackToPics_WhenSteamCmdHasNoDepots()
    {
        var appId = new AppId(746850);

        _storeMock.Setup(s => s.GetAppInfoAsync(appId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SteamAppMetadata?)null);

        // SteamCMD returns empty depots
        _steamCmdMock.Setup(c => c.FetchAppInfoAsync(appId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SteamAppMetadata?)null);

        _storeWebMock.Setup(c => c.FetchStoreDetailsAsync(appId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SteamStoreDetails("Cloudpunk", "https://store.url/header.jpg", "Cloudpunk", null));

        var picsData = new SteamAppMetadata(
            appId,
            name: "Cloudpunk",
            depots: new Dictionary<DepotId, SteamDepotInfo>
            {
                [new DepotId(746851)] = new SteamDepotInfo(new DepotId(746851))
            },
            source: "steam_client");

        _picsMock.Setup(p => p.FetchProductInfoAsync(appId, It.IsAny<AppToken?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(picsData);

        var result = await _resolver.ResolveAppMetadataAsync(appId);

        result.Should().NotBeNull();
        result!.Source.Should().Be("steam_client");
        result.Depots.Should().HaveCount(1);

        _picsMock.Verify(p => p.FetchProductInfoAsync(appId, It.IsAny<AppToken?>(), It.IsAny<CancellationToken>()), Times.Once);
        _storeMock.Verify(s => s.UpsertAppInfoAsync(appId, It.IsAny<SteamAppMetadata>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResolveAppMetadataAsync_ShouldBackfillDepotSizesAndHeaderFromStorefront()
    {
        var appId = new AppId(746850);
        var depotId = new DepotId(746851);

        _storeMock.Setup(s => s.GetAppInfoAsync(appId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SteamAppMetadata?)null);

        var steamCmdData = new SteamAppMetadata(
            appId,
            name: "Cloudpunk",
            headerUrl: "https://old.url/header.jpg",
            depots: new Dictionary<DepotId, SteamDepotInfo>
            {
                [depotId] = new SteamDepotInfo(depotId, size: null)
            },
            source: "steamcmd");

        _steamCmdMock.Setup(c => c.FetchAppInfoAsync(appId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(steamCmdData);

        var storeDetails = new SteamStoreDetails(
            "Cloudpunk",
            "https://new.akamai.store/header.jpg",
            "CloudpunkDir",
            new Dictionary<DepotId, string?> { [depotId] = "104857600" });

        _storeWebMock.Setup(c => c.FetchStoreDetailsAsync(appId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(storeDetails);

        var result = await _resolver.ResolveAppMetadataAsync(appId);

        result.Should().NotBeNull();
        result!.HeaderUrl.Should().Be("https://new.akamai.store/header.jpg");
        result.Depots[depotId].Size.Should().Be("104857600");
    }
}
