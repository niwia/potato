using FluentAssertions;
using Potato.Domain.Vdf;
using Xunit;

namespace Potato.Domain.Tests.Vdf;

public class VdfParserTests
{
    [Fact]
    public void VdfParser_ShouldParseSimpleKeyValues()
    {
        string vdf = """"
        "Key1" "Value1"
        "Key2" "Value2"
        """";

        var root = VdfParser.Parse(vdf);

        root.GetString("Key1").Should().Be("Value1");
        root.GetString("Key2").Should().Be("Value2");
    }

    [Fact]
    public void VdfParser_ShouldHandleLineComments()
    {
        string vdf = """"
        // This is a top-level comment
        "Key1" "Value1" // inline comment
        // Another comment
        "Key2" "Value2"
        """";

        var root = VdfParser.Parse(vdf);

        root.GetString("Key1").Should().Be("Value1");
        root.GetString("Key2").Should().Be("Value2");
    }

    [Fact]
    public void VdfParser_ShouldHandleEscapesInQuotedStrings()
    {
        string vdf = """"
        "Path" "C:\\Games\\My Game\"Special\"\\bin"
        "Newline" "Line1\nLine2"
        """";

        var root = VdfParser.Parse(vdf);

        root.GetString("Path").Should().Be(@"C:\Games\My Game" + "\"Special\"" + @"\bin");
        root.GetString("Newline").Should().Be("Line1\nLine2");
    }

    [Fact]
    public void VdfParser_ShouldParseNestedObjects()
    {
        string vdf = """"
        "Root"
        {
            "ChildKey" "ChildValue"
            "SubObject"
            {
                "DeepKey" "DeepValue"
            }
        }
        """";

        var root = VdfParser.Parse(vdf);

        root.TryGetObject("Root", out var rootObj).Should().BeTrue();
        rootObj.GetString("ChildKey").Should().Be("ChildValue");
        
        rootObj.TryGetObject("SubObject", out var subObj).Should().BeTrue();
        subObj.GetString("DeepKey").Should().Be("DeepValue");
    }

    [Fact]
    public void VdfSerializer_ShouldRoundTripBasicStructure()
    {
        var obj = new VdfObject();
        obj.Set("appid", "746850");
        obj.Set("name", "Cloudpunk");

        var subObj = new VdfObject();
        subObj.Set("language", "english");
        obj.Set("UserConfig", subObj);

        string serialized = VdfSerializer.Serialize(obj, "AppState");
        var reparsed = VdfParser.Parse(serialized);

        reparsed.TryGetObject("AppState", out var appState).Should().BeTrue();
        appState.GetString("appid").Should().Be("746850");
        appState.GetString("name").Should().Be("Cloudpunk");
        appState.GetObject("UserConfig")!.GetString("language").Should().Be("english");
    }
}
