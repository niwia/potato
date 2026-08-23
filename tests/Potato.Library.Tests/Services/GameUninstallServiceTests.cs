using FluentAssertions;
using Moq;
using Potato.Domain.ValueObjects;
using Potato.Library.Models;
using Potato.Library.Services;
using Potato.SlsSteam.Config;
using Potato.SlsSteam.Ipc;
using Xunit;

namespace Potato.Library.Tests.Services;

public class GameUninstallServiceTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _gameFolder;
    private readonly string _acfFile;

    public GameUninstallServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"potato_uninstall_test_{Guid.NewGuid():N}");
        _gameFolder = Path.Combine(_testDir, "steamapps", "common", "TestGame");
        _acfFile = Path.Combine(_testDir, "steamapps", "appmanifest_12345.acf");

        Directory.CreateDirectory(_gameFolder);
        File.WriteAllText(Path.Combine(_gameFolder, "test.exe"), "dummy");
        File.WriteAllText(_acfFile, "AppState { \"appid\" \"12345\" }");
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            try { Directory.Delete(_testDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task UninstallGameAsync_ShouldDeleteGameFilesAndAcf_AndDeregisterFromSls()
    {
        var appId = new AppId(12345);
        var game = new InstalledGame
        {
            AppId = appId,
            Name = "Test Game",
            InstallDir = "TestGame",
            FullGamePath = _gameFolder,
            AcfPath = _acfFile
        };

        var configMock = new Mock<ISlsConfigManager>();
        configMock.Setup(c => c.RemoveAdditionalAppAsync(appId, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var ipcMock = new Mock<ISlsSteamIpcClient>();
        ipcMock.SetupGet(i => i.IsPipeAvailable).Returns(true);
        ipcMock.Setup(i => i.UninstallAppAsync(appId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var uninstaller = new GameUninstallService(configMock.Object, ipcMock.Object);
        bool success = await uninstaller.UninstallGameAsync(game);

        success.Should().BeTrue();
        Directory.Exists(_gameFolder).Should().BeFalse();
        File.Exists(_acfFile).Should().BeFalse();

        configMock.Verify(c => c.RemoveAdditionalAppAsync(appId, It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        ipcMock.Verify(i => i.UninstallAppAsync(appId, It.IsAny<CancellationToken>()), Times.Once);
    }
}
