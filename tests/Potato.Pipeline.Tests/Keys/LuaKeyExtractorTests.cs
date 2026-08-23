using FluentAssertions;
using Potato.Domain.ValueObjects;
using Potato.Pipeline.Keys;
using Xunit;

namespace Potato.Pipeline.Tests.Keys;

public class LuaKeyExtractorTests
{
    [Fact]
    public void ExtractDepotKeys_ShouldParseStandardLuaScript()
    {
        string sampleLua = @"
addappid(1003590, 1) -- Tetris Effect: Connected
addappid(1003591, 1, ""99b924e52d47af4370ef8c397f6e2c53178b23c304560199bd8e2db4220c35f2"") -- Tetris Content
addappid(1723660, 1, ""22898651dd3611d28621644730736f3bb1fd6b960053a45cd79123f2b9a80c91"") -- DLC
addtoken(1003590, ""1234567890abcdef"")
";

        var appId = new AppId(1003590);
        var keys = LuaKeyExtractor.ExtractDepotKeys(sampleLua, appId);
        var token = LuaKeyExtractor.ExtractAppToken(sampleLua);

        keys.Should().HaveCount(2);
        keys[new DepotId(1003591)].Should().Be("99b924e52d47af4370ef8c397f6e2c53178b23c304560199bd8e2db4220c35f2");
        keys[new DepotId(1723660)].Should().Be("22898651dd3611d28621644730736f3bb1fd6b960053a45cd79123f2b9a80c91");

        token.Should().NotBeNull();
    }
}
