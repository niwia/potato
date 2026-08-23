using FluentAssertions;
using Moq;
using Potato.Domain.Acf;
using Potato.Domain.ValueObjects;
using Potato.Downloader.Options;
using Potato.Downloader.Process;
using Potato.Downloader.Progress;
using Potato.ManifestApi.Client;
using Potato.ManifestApi.Models;
using Potato.Pipeline.Keys;
using Potato.Pipeline.Models;
using Potato.Pipeline.Orchestrator;
using Potato.SteamMetadata.Models;
using Potato.SteamMetadata.Resolver;
using Xunit;

namespace Potato.Pipeline.Tests.Orchestrator;

public class InstallGameOrchestratorTests : IDisposable
{
    private readonly string _testLibraryDir;
    private readonly Mock<ISteamMetadataResolver> _metadataResolverMock = new();
    private readonly Mock<IHubcapApiClient> _manifestApiMock = new();
    private readonly Mock<IDepotKeyStore> _depotKeyStoreMock = new();
    private readonly Mock<IDepotDownloaderProcess> _downloaderProcessMock = new();

    public InstallGameOrchestratorTests()
    {
        _testLibraryDir = Path.Combine(Path.GetTempPath(), $"potato_lib_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testLibraryDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testLibraryDir))
        {
            try { Directory.Delete(_testLibraryDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task InstallGameAsync_ShouldExecuteFullPipelineAndWriteAcf()
    {
        var appId = new AppId(1003590);
        var depotId = new DepotId(1003591);
        var gid = new ManifestGid(8212840959240856401);

        // 1. Mock Metadata
        var depots = new Dictionary<DepotId, SteamDepotInfo>
        {
            [depotId] = new SteamDepotInfo(
                depotId,
                name: "Tetris Content",
                manifestGid: gid,
                size: "2048",
                manifests: new Dictionary<string, ManifestGid> { ["public"] = gid })
        };
        var metadata = new SteamAppMetadata(
            appId,
            name: "Tetris Effect: Connected",
            installDir: "TetrisEffectConnected",
            buildId: "24070382",
            depots: depots);

        _metadataResolverMock.Setup(m => m.ResolveAppMetadataAsync(appId, null, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(metadata);

        // 2. Mock Keys
        var keys = new Dictionary<DepotId, string>
        {
            [depotId] = "99b924e52d47af4370ef8c397f6e2c53178b23c304560199bd8e2db4220c35f2"
        };
        _depotKeyStoreMock.Setup(k => k.GetDepotKeysAsync(appId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(keys);

        // 3. Mock Manifests
        var manifestEntry = new ManifestEntry(depotId, gid, new byte[] { 1, 2, 3, 4 });
        var manifestResult = ManifestResolutionResult.CreateSuccess(
            appId,
            "public",
            ManifestTier.Tier1SingleManifest,
            new List<ManifestEntry> { manifestEntry });

        _manifestApiMock.Setup(m => m.ResolveManifestAsync(appId, "public", It.IsAny<IReadOnlyDictionary<DepotId, ManifestGid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(manifestResult);

        // 4. Mock Downloader process creating dummy downloaded file
        _downloaderProcessMock.Setup(d => d.RunAsync(It.IsAny<DepotDownloaderOptions>(), It.IsAny<IProgress<DownloadProgressReport>>(), It.IsAny<CancellationToken>()))
            .Callback<DepotDownloaderOptions, IProgress<DownloadProgressReport>, CancellationToken>((opts, prog, ct) =>
            {
                // Simulate downloaded file in target dir
                string testFile = Path.Combine(opts.DownloadDir, "game.exe");
                File.WriteAllBytes(testFile, new byte[1024]);
            })
            .ReturnsAsync(0);

        var orchestrator = new InstallGameOrchestrator(
            _metadataResolverMock.Object,
            _manifestApiMock.Object,
            _depotKeyStoreMock.Object,
            () => _downloaderProcessMock.Object);

        var request = new InstallRequest(appId, _testLibraryDir);
        var progressReports = new List<InstallProgressReport>();
        var progress = new Progress<InstallProgressReport>(r => progressReports.Add(r));

        var result = await orchestrator.InstallGameAsync(request, progress);

        result.Success.Should().BeTrue();
        result.GameName.Should().Be("Tetris Effect: Connected");
        result.InstallDir.Should().Be("TetrisEffectConnected");
        result.TotalBytesOnDisk.Should().Be(1024);

        // Verify ACF file was written to disk
        string acfPath = Path.Combine(_testLibraryDir, "steamapps", "appmanifest_1003590.acf");
        File.Exists(acfPath).Should().BeTrue();

        var acfState = AcfManager.LoadFromFile(acfPath);
        acfState.Should().NotBeNull();
        acfState!.AppId.Should().Be(appId);
        acfState.Name.Should().Be("Tetris Effect: Connected");
        acfState.InstallDir.Should().Be("TetrisEffectConnected");
        acfState.BuildId.Should().Be("24070382");
        acfState.InstalledDepots.Should().HaveCount(1);
        acfState.InstalledDepots[0].DepotId.Should().Be(depotId);
        acfState.InstalledDepots[0].ManifestGid.Should().Be(gid);

        // Verify progress step sequence
        progressReports.Should().Contain(r => r.Step == InstallStep.ResolvingMetadata);
        progressReports.Should().Contain(r => r.Step == InstallStep.ResolvingKeys);
        progressReports.Should().Contain(r => r.Step == InstallStep.ResolvingManifests);
        progressReports.Should().Contain(r => r.Step == InstallStep.DownloadingDepots);
        progressReports.Should().Contain(r => r.Step == InstallStep.FinalizingAcf);
        progressReports.Should().Contain(r => r.Step == InstallStep.Completed);
    }

    [Fact]
    public async Task InstallGameAsync_ShouldFailEarly_WhenMetadataResolutionFails()
    {
        var appId = new AppId(9999999);

        _metadataResolverMock.Setup(m => m.ResolveAppMetadataAsync(appId, null, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SteamAppMetadata?)null);

        var orchestrator = new InstallGameOrchestrator(
            _metadataResolverMock.Object,
            _manifestApiMock.Object,
            _depotKeyStoreMock.Object,
            () => _downloaderProcessMock.Object);

        var request = new InstallRequest(appId, _testLibraryDir);
        var result = await orchestrator.InstallGameAsync(request);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Metadata resolution failed");

        // Verified downloader process was never invoked
        _downloaderProcessMock.Verify(d => d.RunAsync(It.IsAny<DepotDownloaderOptions>(), It.IsAny<IProgress<DownloadProgressReport>>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
