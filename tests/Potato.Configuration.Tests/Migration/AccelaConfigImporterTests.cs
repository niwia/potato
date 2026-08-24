using FluentAssertions;
using Potato.Configuration.Migration;
using Xunit;

namespace Potato.Configuration.Tests.Migration;

public class AccelaConfigImporterTests
{
    [Fact]
    public void ImportFromIni_ShouldMigrateAllKnownKeysCorrectly()
    {
        string sampleIni = """
        [General]
        morrenus_api_key = test-legacy-key-999
        isp_bypass_hubcap = false
        wirecutter_url = https://wirecutter.proxy.dev
        max_downloads = 12
        use_lancache = false
        auto_skip_single_choice = true
        smart_depot_selection = true
        filter_soundtracks = true
        hide_macos_depots = true
        library_mode = true
        default_download_directory = /mnt/games/steamapps
        sls_config_management = true
        prompt_steam_restart = true
        auto_install_goldberg = true
        auto_apply_goldberg = false
        check_updates_on_boot = false
        update_check_interval_minutes = 90
        library_sort_option = size_on_disk
        accent_color = #a1c9fd
        dark_mode = true
        material_preset = ocean
        font = Roboto
        font-size = 11
        nerd_mode = true
        log_filter_level = DEBUG
        enable_remote_web_ui = true
        web_ui_port = 9000
        workshop_cell_id = 1234
        workshop_max_downloads = 6

        [exclude_from_update_all]
        1004640 = true
        2215200 = true
        999999 = false
        """;

        var settings = AccelaConfigImporter.ImportFromIni(sampleIni);

        settings.Should().NotBeNull();
        settings.Api.HubcapApiKey.Should().Be("test-legacy-key-999");
        settings.Api.UseIspBypass.Should().BeFalse();
        settings.Api.CustomWirecutterUrl.Should().Be("https://wirecutter.proxy.dev");

        settings.Download.MaxDownloadsPerJob.Should().Be(12);
        settings.Download.UseLanCache.Should().BeFalse();
        settings.Download.AutoSkipSingleChoice.Should().BeTrue();
        settings.Download.SmartDepotSelection.Should().BeTrue();
        settings.Download.FilterSoundtracks.Should().BeTrue();
        settings.Download.FilterMacOsDepots.Should().BeTrue();
        settings.Download.LimitToSteamLibraries.Should().BeTrue();
        settings.Download.DefaultDownloadDirectory.Should().Be("/mnt/games/steamapps");

        settings.SlsSteam.EnableSlsIntegration.Should().BeTrue();
        settings.SlsSteam.PromptSteamRestart.Should().BeTrue();
        settings.SlsSteam.AutoInstallGoldberg.Should().BeTrue();
        settings.SlsSteam.AutoApplyGoldberg.Should().BeFalse();

        settings.Library.CheckUpdatesOnStartup.Should().BeFalse();
        settings.Library.UpdateCheckIntervalMinutes.Should().Be(90);
        settings.Library.LibrarySortOption.Should().Be("size_on_disk");
        settings.Library.ExcludedFromUpdateAll.Should().Contain(1004640u);
        settings.Library.ExcludedFromUpdateAll.Should().Contain(2215200u);
        settings.Library.ExcludedFromUpdateAll.Should().NotContain(999999u);

        settings.Appearance.AccentColor.Should().Be("#a1c9fd");
        settings.Appearance.Theme.Should().Be("Dark");
        settings.Appearance.PresetName.Should().Be("ocean");
        settings.Appearance.FontFamily.Should().Be("Roboto");
        settings.Appearance.FontSize.Should().Be(11);
        settings.Appearance.NerdMode.Should().BeTrue();

        settings.Advanced.LogLevel.Should().Be("DEBUG");
        settings.Advanced.EnableRemoteWebUi.Should().BeTrue();
        settings.Advanced.WebUiPort.Should().Be(9000);
        settings.Advanced.WorkshopCellId.Should().Be("1234");
        settings.Advanced.WorkshopMaxDownloads.Should().Be(6);
    }
}
