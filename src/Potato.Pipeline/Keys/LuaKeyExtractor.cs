using System.Text.RegularExpressions;
using Potato.Domain.ValueObjects;

namespace Potato.Pipeline.Keys;

/// <summary>
/// Extracts depot decryption keys and app tokens from legacy/classic Steam Lua files.
/// </summary>
public static class LuaKeyExtractor
{
    private static readonly Regex AddAppIdRegex = new(
        @"addappid\(\s*(\d+)\s*,\s*1\s*,\s*[""']([^""']+)[""']",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AddTokenRegex = new(
        @"addtoken\(\s*\d+\s*,\s*[""']([^""']+)[""']",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static Dictionary<DepotId, string> ExtractDepotKeys(string luaContent, AppId appId)
    {
        var result = new Dictionary<DepotId, string>();
        if (string.IsNullOrWhiteSpace(luaContent)) return result;

        foreach (Match match in AddAppIdRegex.Matches(luaContent))
        {
            if (match.Groups.Count >= 3 &&
                DepotId.TryParse(match.Groups[1].Value, out var depotId) &&
                depotId.Value != appId.Value)
            {
                result[depotId] = match.Groups[2].Value.Trim();
            }
        }

        return result;
    }

    public static AppToken? ExtractAppToken(string luaContent)
    {
        if (string.IsNullOrWhiteSpace(luaContent)) return null;

        var match = AddTokenRegex.Match(luaContent);
        if (match.Success && match.Groups.Count >= 2)
        {
            if (AppToken.TryParse(match.Groups[1].Value.Trim(), out var token))
            {
                return token;
            }
        }

        return null;
    }
}
