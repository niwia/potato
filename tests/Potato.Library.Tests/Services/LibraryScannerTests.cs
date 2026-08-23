using FluentAssertions;
using Potato.Domain.Acf;
using Potato.Domain.Models;
using Potato.Domain.ValueObjects;
using Potato.Library.Services;
using Xunit;

namespace Potato.Library.Tests.Services;

public class LibraryScannerTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _steamAppsDir;
    private readonly string _commonDir;

    public LibraryScannerTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"potato_lib_test_{Guid.NewGuid():N}");
        _steamAppsDir = Path.Combine(_testDir, "steamapps");
        _commonDir = Path.Combine(_steamAppsDir, "common");

        Directory.CreateDirectory(_commonDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            try { Directory.Delete(_testDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task ScanLibrariesAsync_ShouldDiscoverInstalledGameWithAcfManifest()
    {
        var appId = new AppId(1003590);
        string installDir = "Tetris Effect Connected";
        string gameFolder = Path.Combine(_commonDir, installDir);
        Directory.CreateDirectory(gameFolder);
        await File.WriteAllTextAsync(Path.Combine(gameFolder, "TetrisEffect.exe"), "dummy binary data");

        var acfState = new AcfAppState
        {
            AppId = appId,
            Name = "Tetris® Effect: Connected",
            InstallDir = installDir,
            BuildId = "123456",
            SizeOnDisk = 1048576,
            InstalledDepots = new List<InstalledDepotInfo>
            {
                new(new DepotId(1723660), new ManifestGid(9876543210123456), 1048576)
            }
        };

        string acfPath = Path.Combine(_steamAppsDir, $"appmanifest_{appId}.acf");
        AcfManager.SaveToFile(acfState, acfPath);

        var scanner = new LibraryScanner();
        var result = await scanner.ScanLibrariesAsync(new[] { _steamAppsDir });

        result.InstalledGames.Should().HaveCount(1);
        var game = result.InstalledGames[0];
        game.AppId.Should().Be(appId);
        game.Name.Should().Be("Tetris® Effect: Connected");
        game.InstallDir.Should().Be(installDir);
        game.BuildId.Should().Be("123456");
        game.SizeOnDisk.Should().Be(1048576);
        game.InstalledDepots.Should().HaveCount(1);
        game.AcfPath.Should().Be(acfPath);
    }
}
