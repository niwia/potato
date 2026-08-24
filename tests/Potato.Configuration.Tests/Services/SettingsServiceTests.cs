using FluentAssertions;
using Potato.Configuration.Models;
using Potato.Configuration.Services;
using Xunit;

namespace Potato.Configuration.Tests.Services;

public class SettingsServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _tempSettingsFile;

    public SettingsServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "potato_settings_tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _tempSettingsFile = Path.Combine(_tempDir, "settings.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task LoadAsync_WhenFileDoesNotExist_ShouldCreateDefaultSettingsFile()
    {
        var service = new SettingsService(_tempSettingsFile, autoMigrateLegacy: false);

        var settings = await service.LoadAsync();

        settings.Should().NotBeNull();
        settings.Api.UseIspBypass.Should().BeTrue();
        settings.Download.MaxDownloadsPerJob.Should().Be(8);
        settings.Download.MaxConcurrentQueueJobs.Should().Be(2);
        settings.SlsSteam.EnableSlsIntegration.Should().BeTrue();
        settings.Appearance.Theme.Should().Be("Dark");

        File.Exists(_tempSettingsFile).Should().BeTrue();
    }

    [Fact]
    public async Task SaveAsync_ShouldPersistAllSectionsAtomically()
    {
        var service = new SettingsService(_tempSettingsFile, autoMigrateLegacy: false);

        var settings = new PotatoSettings
        {
            Api = new ApiSettings
            {
                HubcapApiKey = "test-api-key-12345",
                UseIspBypass = false,
                CustomWirecutterUrl = "https://custom.worker.dev"
            },
            Download = new DownloadSettings
            {
                MaxDownloadsPerJob = 16,
                MaxConcurrentQueueJobs = 4,
                DefaultDownloadDirectory = "/home/user/games"
            },
            Appearance = new AppearanceSettings
            {
                Theme = "Light",
                AccentColor = "#FF5722",
                NerdMode = true
            }
        };

        await service.SaveAsync(settings);

        var loadedService = new SettingsService(_tempSettingsFile, autoMigrateLegacy: false);
        var loaded = await loadedService.LoadAsync();

        loaded.Api.HubcapApiKey.Should().Be("test-api-key-12345");
        loaded.Api.UseIspBypass.Should().BeFalse();
        loaded.Api.CustomWirecutterUrl.Should().Be("https://custom.worker.dev");
        loaded.Download.MaxDownloadsPerJob.Should().Be(16);
        loaded.Download.MaxConcurrentQueueJobs.Should().Be(4);
        loaded.Download.DefaultDownloadDirectory.Should().Be("/home/user/games");
        loaded.Appearance.Theme.Should().Be("Light");
        loaded.Appearance.AccentColor.Should().Be("#FF5722");
        loaded.Appearance.NerdMode.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateAsync_ShouldMutateSettings_AndFireSettingsChangedEvent()
    {
        var service = new SettingsService(_tempSettingsFile, autoMigrateLegacy: false);
        await service.LoadAsync();

        SettingsChangedEventArgs? eventArgs = null;
        service.SettingsChanged += (s, e) =>
        {
            eventArgs = e;
        };

        var updated = await service.UpdateAsync(s =>
        {
            s.Download.MaxDownloadsPerJob = 24;
            s.Appearance.Theme = "OceanDark";
        });

        updated.Download.MaxDownloadsPerJob.Should().Be(24);
        updated.Appearance.Theme.Should().Be("OceanDark");

        eventArgs.Should().NotBeNull();
        eventArgs!.OldSettings.Download.MaxDownloadsPerJob.Should().Be(8);
        eventArgs.NewSettings.Download.MaxDownloadsPerJob.Should().Be(24);
    }

    [Fact]
    public async Task ResetToDefaultsAsync_ShouldRestoreDefaultsAndSave()
    {
        var service = new SettingsService(_tempSettingsFile, autoMigrateLegacy: false);
        await service.SaveAsync(new PotatoSettings
        {
            Download = new DownloadSettings { MaxDownloadsPerJob = 30 },
            Appearance = new AppearanceSettings { NerdMode = true }
        });

        var reset = await service.ResetToDefaultsAsync();

        reset.Download.MaxDownloadsPerJob.Should().Be(8);
        reset.Appearance.NerdMode.Should().BeFalse();
    }

    [Fact]
    public async Task ImportFromLegacyConfigAsync_ShouldImportAndPersist()
    {
        string dummyLegacyIni = Path.Combine(_tempDir, "dummy_ACCELA.conf");
        await File.WriteAllTextAsync(dummyLegacyIni, "[General]\nmorrenus_api_key = imported-key-777\nmax_downloads = 14\n");

        var service = new SettingsService(_tempSettingsFile, autoMigrateLegacy: false);
        var imported = await service.ImportFromLegacyConfigAsync(dummyLegacyIni);

        imported.Api.HubcapApiKey.Should().Be("imported-key-777");
        imported.Download.MaxDownloadsPerJob.Should().Be(14);

        var reloadedService = new SettingsService(_tempSettingsFile, autoMigrateLegacy: false);
        var reloaded = await reloadedService.LoadAsync();
        reloaded.Api.HubcapApiKey.Should().Be("imported-key-777");
        reloaded.Download.MaxDownloadsPerJob.Should().Be(14);
    }
}
