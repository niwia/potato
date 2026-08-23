using FluentAssertions;
using Potato.Domain.ValueObjects;
using Xunit;

namespace Potato.Domain.Tests.ValueObjects;

public class ValueObjectTests
{
    [Fact]
    public void AppId_ShouldParseValidNumbers()
    {
        // Act
        var appId = AppId.Parse("746850");

        // Assert
        appId.Value.Should().Be(746850u);
        appId.IsValid.Should().BeTrue();
        appId.ToString().Should().Be("746850");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("0")]
    [InlineData("-123")]
    [InlineData("invalid")]
    [InlineData(null)]
    public void AppId_TryParse_ShouldReturnFalse_ForInvalidInputs(string? input)
    {
        // Act
        bool result = AppId.TryParse(input, out var appId);

        // Assert
        result.Should().BeFalse();
        appId.Should().Be(AppId.Empty);
        appId.IsValid.Should().BeFalse();
    }

    [Fact]
    public void AppId_ShouldSupportEqualityAndOperators()
    {
        var id1 = new AppId(480);
        var id2 = AppId.From(480);
        var id3 = new AppId(746850);

        (id1 == id2).Should().BeTrue();
        (id1 == id3).Should().BeFalse();
        ((uint)id1).Should().Be(480u);
        ((AppId)480u).Should().Be(id1);
    }

    [Fact]
    public void DepotId_ShouldParseAndSupportEquality()
    {
        var depotId = DepotId.Parse("746851");
        depotId.Value.Should().Be(746851u);
        depotId.ToString().Should().Be("746851");

        DepotId.TryParse("0", out var empty).Should().BeFalse();
        empty.Should().Be(DepotId.Empty);
    }

    [Fact]
    public void ManifestGid_ShouldParse64BitUnsignedInts()
    {
        const string gidStr = "5225699216215765938";
        var gid = ManifestGid.Parse(gidStr);

        gid.Value.Should().Be(5225699216215765938ul);
        gid.ToString().Should().Be(gidStr);

        ManifestGid.TryParse("invalid", out var empty).Should().BeFalse();
        empty.Should().Be(ManifestGid.Empty);
    }

    [Fact]
    public void AppToken_ShouldParseValid64BitTokens()
    {
        const string tokenStr = "123456789012345678";
        var token = AppToken.Parse(tokenStr);

        token.Value.Should().Be(123456789012345678ul);
        token.ToString().Should().Be(tokenStr);

        AppToken.TryParse("invalid", out var empty).Should().BeFalse();
        empty.Should().Be(AppToken.Empty);
    }
}
