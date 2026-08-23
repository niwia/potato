using FluentAssertions;
using Potato.SlsSteam.Config;
using Xunit;

namespace Potato.SlsSteam.Tests.Config;

public class SlsConfigHealerTests
{
    [Fact]
    public void NormalizeSteamId_ShouldConvert32BitAccountIdToSteamId64()
    {
        // 32-bit account ID: 123456789 -> SteamID64: 76561198083722517
        string accountId = "123456789";
        string steamId64 = SlsConfigHealer.NormalizeSteamId(accountId);

        steamId64.Should().Be("76561198083722517");
    }

    [Fact]
    public void NormalizeSteamId_ShouldLeaveValidSteamId64Untouched()
    {
        string validSteamId = "76561199083839651";
        string result = SlsConfigHealer.NormalizeSteamId(validSteamId);

        result.Should().Be(validSteamId);
    }

    [Fact]
    public void ParseAndHeal_ShouldRecoverMalformedAdditionalApps_AndDeduplicate()
    {
        string rawMalformedYaml = @"
DisableFamilyShareLock: yes
AdditionalApps:
  - 1003590 # Tetris Effect Old
- 1004640 # Final Fantasy
  1005300 # Jackbox
  -1003590 # Tetris Effect New
API: no
LogLevels: 0x0
";

        var model = SlsConfigHealer.ParseAndHeal(rawMalformedYaml);

        // Deduplication: 1003590 appears twice, last occurrence wins
        model.AdditionalApps.Should().HaveCount(3);
        model.AdditionalApps.Should().Contain(a => a.Value == "1003590" && a.InlineComment == "Tetris Effect New");
        model.AdditionalApps.Should().Contain(a => a.Value == "1004640" && a.InlineComment == "Final Fantasy");
        model.AdditionalApps.Should().Contain(a => a.Value == "1005300" && a.InlineComment == "Jackbox");

        // Prerequisite enforcement: API enabled and LogLevels Once bit (0x2) set
        model.Api.Should().BeTrue();
        model.LogLevels.Should().Be("0x2");
    }

    [Fact]
    public void Serialize_ShouldGenerateCleanYamlStructure()
    {
        var model = new SlsConfigModel();
        model.AdditionalApps.Add(new SlsConfigEntry(null, "1003590", "Tetris Effect"));
        model.AppTokens["1003590"] = new SlsConfigEntry("1003590", "token123", "Main Token");
        model.FakeAppIds["1003590"] = new SlsConfigEntry("1003590", "480", "Online Spacewar");

        string yaml = SlsConfigHealer.Serialize(model);

        yaml.Should().Contain("AdditionalApps:\n  - 1003590 # Tetris Effect");
        yaml.Should().Contain("AppTokens:\n  1003590: token123 # Main Token");
        yaml.Should().Contain("FakeAppIds:\n  1003590: 480 # Online Spacewar");
        yaml.Should().Contain("API: yes");
        yaml.Should().Contain("LogLevels: 0x2");
    }
}
