using FluentAssertions;
using Potato.Domain.Acf;
using Potato.Domain.Models;
using Potato.Domain.Tests.TestData;
using Potato.Domain.ValueObjects;
using Xunit;

namespace Potato.Domain.Tests.Acf;

public class AcfManagerTests
{
    [Fact]
    public void Parse_CloudpunkSample_ShouldPopulateAllFieldsCorrectly()
    {
        // Act
        var state = AcfManager.Parse(SampleAcfFiles.CloudpunkAcf);

        // Assert
        state.AppId.Value.Should().Be(746850u);
        state.Universe.Should().Be(1);
        state.Name.Should().Be("Cloudpunk");
        state.StateFlags.Should().Be(4);
        state.InstallDir.Should().Be("Cloudpunk");
        state.BuildId.Should().Be("8245592");
        state.SizeOnDisk.Should().Be(7087768157ul);
        state.LastOwner.Should().Be(76561199083839651ul);
        state.BytesToDownload.Should().Be(69026464ul);
        state.BytesDownloaded.Should().Be(69026464ul);

        state.InstalledDepots.Should().HaveCount(1);
        var depot = state.InstalledDepots[0];
        depot.DepotId.Value.Should().Be(746851u);
        depot.ManifestGid.Value.Should().Be(5225699216215765938ul);
        depot.SizeBytes.Should().Be(7087768157ul);

        state.UserConfig.Should().ContainKey("language").WhoseValue.Should().Be("english");
        state.MountedConfig.Should().ContainKey("language").WhoseValue.Should().Be("english");
    }

    [Fact]
    public void RoundTrip_CloudpunkSample_ShouldPreserveExactSemanticValues()
    {
        // 1. Parse original
        var originalState = AcfManager.Parse(SampleAcfFiles.CloudpunkAcf);

        // 2. Serialize to VDF text
        string serializedVdf = AcfManager.Serialize(originalState);

        // 3. Re-parse serialized VDF
        var reparsedState = AcfManager.Parse(serializedVdf);

        // 4. Assert full equality
        reparsedState.AppId.Should().Be(originalState.AppId);
        reparsedState.Name.Should().Be(originalState.Name);
        reparsedState.InstallDir.Should().Be(originalState.InstallDir);
        reparsedState.BuildId.Should().Be(originalState.BuildId);
        reparsedState.SizeOnDisk.Should().Be(originalState.SizeOnDisk);
        reparsedState.StateFlags.Should().Be(originalState.StateFlags);
        reparsedState.LastOwner.Should().Be(originalState.LastOwner);
        reparsedState.InstalledDepots.Should().BeEquivalentTo(originalState.InstalledDepots);
        reparsedState.UserConfig.Should().BeEquivalentTo(originalState.UserConfig);
        reparsedState.MountedConfig.Should().BeEquivalentTo(originalState.MountedConfig);
    }

    [Fact]
    public void Parse_MultiDepotSample_ShouldParseInstallScriptsAndPlatformOverrides()
    {
        var state = AcfManager.Parse(SampleAcfFiles.MultiDepotAcf);

        state.AppId.Value.Should().Be(228980u);
        state.InstalledDepots.Should().HaveCount(3);
        state.InstallScripts.Should().HaveCount(2);
        state.InstallScripts.Should().ContainKey("228981");

        state.UserConfig.Should().ContainKey("platform_override_dest").WhoseValue.Should().Be("linux");
        state.UserConfig.Should().ContainKey("platform_override_source").WhoseValue.Should().Be("windows");

        // Test Round-Trip
        string serialized = AcfManager.Serialize(state);
        var roundTripped = AcfManager.Parse(serialized);

        roundTripped.InstalledDepots.Should().HaveCount(3);
        roundTripped.InstallScripts.Should().HaveCount(2);
        roundTripped.UserConfig.Should().BeEquivalentTo(state.UserConfig);
    }

    [Fact]
    public void RoundTrip_WithCustomFields_ShouldPreserveUnmappedFields()
    {
        var state = AcfManager.Parse(SampleAcfFiles.WithCustomFieldsAcf);

        state.AdditionalFields.Should().ContainKey("CustomKey");
        state.AdditionalFields.Should().ContainKey("CustomObject");

        string serialized = AcfManager.Serialize(state);
        var roundTripped = AcfManager.Parse(serialized);

        roundTripped.AdditionalFields.Should().ContainKey("CustomKey");
        roundTripped.AdditionalFields.Should().ContainKey("CustomObject");
    }

    [Fact]
    public void ToGame_And_FromGame_ShouldMapCorrectly()
    {
        var originalState = AcfManager.Parse(SampleAcfFiles.CloudpunkAcf);

        // Convert AcfAppState -> Game
        Game game = AcfManager.ToGame(originalState, branch: "public");
        game.AppId.Value.Should().Be(746850u);
        game.Name.Should().Be("Cloudpunk");
        game.InstallDir.Should().Be("Cloudpunk");
        game.BuildId.Should().Be("8245592");
        game.Branch.Should().Be("public");
        game.InstalledDepots.Should().HaveCount(1);
        game.InstalledDepots[0].DepotId.Value.Should().Be(746851u);

        // Convert Game -> AcfAppState
        var newState = AcfManager.FromGame(game, lastOwner: 76561199083839651ul, sizeOnDisk: 7087768157ul);
        newState.AppId.Should().Be(game.AppId);
        newState.Name.Should().Be(game.Name);
        newState.InstallDir.Should().Be(game.InstallDir);
        newState.BuildId.Should().Be(game.BuildId);
        newState.InstalledDepots.Should().HaveCount(1);

        // Ensure resulting ACF serializes and parses cleanly
        string vdf = AcfManager.Serialize(newState);
        vdf.Should().Contain("\"appid\"\t\t\"746850\"");
        vdf.Should().Contain("\"name\"\t\t\"Cloudpunk\"");
        vdf.Should().Contain("\"746851\"");
    }

    [Fact]
    public void LoadFromFile_And_SaveToFile_ShouldRoundTripViaFileSystem()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"appmanifest_test_{Guid.NewGuid():N}.acf");
        try
        {
            File.WriteAllText(tempFile, SampleAcfFiles.CloudpunkAcf);

            var loaded = AcfManager.LoadFromFile(tempFile);
            loaded.AppId.Value.Should().Be(746850u);
            loaded.Name.Should().Be("Cloudpunk");

            string savedTempFile = Path.Combine(Path.GetTempPath(), $"appmanifest_saved_{Guid.NewGuid():N}.acf");
            try
            {
                AcfManager.SaveToFile(loaded, savedTempFile);
                File.Exists(savedTempFile).Should().BeTrue();

                var reloaded = AcfManager.LoadFromFile(savedTempFile);
                reloaded.AppId.Should().Be(loaded.AppId);
                reloaded.Name.Should().Be(loaded.Name);
                reloaded.InstalledDepots.Should().BeEquivalentTo(loaded.InstalledDepots);
            }
            finally
            {
                if (File.Exists(savedTempFile)) File.Delete(savedTempFile);
            }
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}
