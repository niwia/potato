using System.Text;
using System.Text.RegularExpressions;

namespace Potato.SlsSteam.Config;

/// <summary>
/// Implements ASSFixer healing, malformed YAML recovery, deduplication, and ID conversions for SLSsteam config.yaml.
/// </summary>
public static class SlsConfigHealer
{
    private const ulong SteamId64Base = 76561197960265728UL; // 0x0110000100000000UL
    private const ulong Max32BitAccountId = 4294967295UL;

    private static readonly Regex SectionHeaderRegex = new(@"^([A-Za-z][A-Za-z0-9_]*)\s*:\s*(.*)$", RegexOptions.Compiled);
    private static readonly Regex NestedKeyRegex = new(@"^\s*([A-Za-z0-9_]+)\s*:\s*(.*)$", RegexOptions.Compiled);
    private static readonly Regex ListItemRegex = new(@"^\s*-\s*([^\r\n#]+?)(?:\s*#\s*(.*))?$", RegexOptions.Compiled);
    private static readonly Regex MapEntryRegex = new(@"^\s*([^:\r\n#]+)\s*:\s*([^#\r\n]*)(?:\s*#\s*(.*))?$", RegexOptions.Compiled);
    private static readonly Regex SalvageListRegex = new(@"^\s*(?:-\s*)?(\d+)(?:\s*#\s*(.*))?$", RegexOptions.Compiled);
    private static readonly Regex SalvageMapRegex = new(@"^\s*(\d+)\s*:\s*(\S+)(?:\s*#\s*(.*))?$", RegexOptions.Compiled);

    /// <summary>
    /// Converts a 32-bit AccountID to a public 64-bit SteamID64 if applicable.
    /// </summary>
    public static string NormalizeSteamId(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return input;

        var trimmed = input.Trim().Trim('"', '\'');
        if (ulong.TryParse(trimmed, out var idVal) && idVal > 0 && idVal <= Max32BitAccountId)
        {
            return (SteamId64Base + idVal).ToString();
        }

        return trimmed;
    }

    /// <summary>
    /// Parses and heals a raw YAML config string, applying ASSFixer recovery rules.
    /// </summary>
    public static SlsConfigModel ParseAndHeal(string yamlContent)
    {
        var model = new SlsConfigModel();
        if (string.IsNullOrWhiteSpace(yamlContent))
        {
            return model;
        }

        string[] lines = yamlContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        int i = 0;
        int n = lines.Length;

        while (i < n)
        {
            string line = lines[i];
            string trimmed = line.Trim();

            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#"))
            {
                i++;
                continue;
            }

            var match = SectionHeaderRegex.Match(line);
            if (!match.Success)
            {
                i++;
                continue;
            }

            string sectionName = match.Groups[1].Value.Trim();
            string inlineVal = match.Groups[2].Value.Trim();

            if (!string.IsNullOrEmpty(inlineVal) && !inlineVal.StartsWith("#"))
            {
                // Scalar value
                ApplyScalarValue(model, sectionName, inlineVal);
                i++;
                continue;
            }

            // Block section: read child lines until next unindented top-level section header
            int start = i + 1;
            int end = start;
            while (end < n)
            {
                string nextLine = lines[end];
                if (!nextLine.StartsWith(" ") && !nextLine.StartsWith("\t") && SectionHeaderRegex.IsMatch(nextLine))
                {
                    break;
                }
                end++;
            }

            var blockLines = lines[start..end];
            ApplyBlockSection(model, sectionName, blockLines);
            i = end;
        }

        // Apply global deduplication and prerequisite enforcement
        DeduplicateModel(model);
        EnsurePrerequisites(model);

        return model;
    }

    private static void ApplyScalarValue(SlsConfigModel model, string key, string valueWithComment)
    {
        string val = valueWithComment.Split('#')[0].Trim();

        switch (key)
        {
            case "DisableFamilyShareLock":
                model.DisableFamilyShareLock = ParseYamlBool(val, true);
                break;
            case "UseWhitelist":
                model.UseWhitelist = ParseYamlBool(val, false);
                break;
            case "MaxSchemaTries":
                if (int.TryParse(val, out int mst)) model.MaxSchemaTries = mst;
                break;
            case "SafeMode":
                model.SafeMode = ParseYamlBool(val, true);
                break;
            case "WarnHashMissmatch":
                model.WarnHashMissmatch = ParseYamlBool(val, true);
                break;
            case "NotifyInit":
                model.NotifyInit = ParseYamlBool(val, false);
                break;
            case "API":
                model.Api = ParseYamlBool(val, true);
                break;
            case "DisableCloud":
                model.DisableCloud = ParseYamlBool(val, true);
                break;
            case "DisableUpdates":
                model.DisableUpdates = ParseYamlBool(val, false);
                break;
            case "FakeName":
                model.FakeName = val.Trim('"', '\'');
                break;
            case "FakeEmail":
                model.FakeEmail = val.Trim('"', '\'');
                break;
            case "FakeWalletBalance":
                if (int.TryParse(val, out int fwb)) model.FakeWalletBalance = fwb;
                break;
            case "LogLevels":
                model.LogLevels = val;
                break;
            case "LogLevel": // Old enum format
                if (int.TryParse(val, out int ll) && ll == 0) model.LogLevels = "0x2";
                break;
            case "DumpClientInterfaces":
                model.DumpClientInterfaces = ParseYamlBool(val, false);
                break;
            case "ExtendedLogging":
                model.ExtendedLogging = ParseYamlBool(val, false);
                break;
            default:
                model.CustomSections[key] = valueWithComment;
                break;
        }
    }

    private static void ApplyBlockSection(SlsConfigModel model, string sectionName, string[] blockLines)
    {
        switch (sectionName)
        {
            case "AdditionalApps":
                model.AdditionalApps.AddRange(ParseListWithSalvage(blockLines));
                break;
            case "AppIds":
                model.AppIds.AddRange(ParseListWithSalvage(blockLines));
                break;
            case "DepotBlacklist":
                model.DepotBlacklist.AddRange(ParseListWithSalvage(blockLines));
                break;
            case "AppTokens":
                MergeMap(model.AppTokens, ParseMapWithSalvage(blockLines));
                break;
            case "FakeAppIds":
                MergeMap(model.FakeAppIds, ParseMapWithSalvage(blockLines));
                break;
            case "ManifestIds":
                MergeMap(model.ManifestIds, ParseMapWithSalvage(blockLines));
                break;
            case "GameTitles":
                MergeMap(model.GameTitles, ParseMapWithSalvage(blockLines));
                break;
            case "SubscriptionTimestamps":
                MergeMap(model.SubscriptionTimestamps, ParseMapWithSalvage(blockLines));
                break;
            case "SteamIdOverride":
                var overrides = ParseMapWithSalvage(blockLines);
                foreach (var (k, v) in overrides)
                {
                    string normK = NormalizeSteamId(k);
                    string normV = NormalizeSteamId(v.Value);
                    model.SteamIdOverride[normK] = new SlsConfigEntry(normK, normV, v.InlineComment);
                }
                break;
            case "DlcData":
                ParseDlcData(model.DlcData, blockLines);
                break;
            case "DenuvoGames":
                ParseDenuvoGames(model.DenuvoGames, blockLines);
                break;
            case "IdleStatus":
                foreach (var line in blockLines)
                {
                    var m = MapEntryRegex.Match(line);
                    if (m.Success)
                    {
                        model.IdleStatus[m.Groups[1].Value.Trim()] = m.Groups[2].Value.Trim();
                    }
                }
                break;
            default:
                model.CustomSections[sectionName] = string.Join("\n", blockLines);
                break;
        }
    }

    private static List<SlsConfigEntry> ParseListWithSalvage(string[] lines)
    {
        var list = new List<SlsConfigEntry>();
        foreach (string line in lines)
        {
            string trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#")) continue;

            var match = ListItemRegex.Match(line);
            if (match.Success)
            {
                string val = match.Groups[1].Value.Trim();
                string? comment = match.Groups[2].Success ? match.Groups[2].Value.Trim() : null;
                list.Add(new SlsConfigEntry(null, val, comment));
                continue;
            }

            // Salvage broken line (e.g. without '-' or misaligned)
            var salvage = SalvageListRegex.Match(line);
            if (salvage.Success)
            {
                string val = salvage.Groups[1].Value.Trim();
                string? comment = salvage.Groups[2].Success ? salvage.Groups[2].Value.Trim() : null;
                list.Add(new SlsConfigEntry(null, val, comment));
            }
        }
        return list;
    }

    private static Dictionary<string, SlsConfigEntry> ParseMapWithSalvage(string[] lines)
    {
        var map = new Dictionary<string, SlsConfigEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (string line in lines)
        {
            string trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#")) continue;

            var match = MapEntryRegex.Match(line);
            if (match.Success)
            {
                string k = match.Groups[1].Value.Trim();
                string v = match.Groups[2].Value.Trim();
                string? comment = match.Groups[3].Success ? match.Groups[3].Value.Trim() : null;
                if (!string.IsNullOrEmpty(k) && !string.IsNullOrEmpty(v))
                {
                    map[k] = new SlsConfigEntry(k, v, comment);
                    continue;
                }
            }

            var salvage = SalvageMapRegex.Match(line);
            if (salvage.Success)
            {
                string k = salvage.Groups[1].Value.Trim();
                string v = salvage.Groups[2].Value.Trim();
                string? comment = salvage.Groups[3].Success ? salvage.Groups[3].Value.Trim() : null;
                map[k] = new SlsConfigEntry(k, v, comment);
            }
        }
        return map;
    }

    private static void ParseDlcData(Dictionary<string, Dictionary<string, SlsConfigEntry>> target, string[] lines)
    {
        string? currentAppId = null;
        foreach (string line in lines)
        {
            string trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#")) continue;

            if (line.StartsWith("  ") && !line.StartsWith("    "))
            {
                var m = NestedKeyRegex.Match(line);
                if (m.Success)
                {
                    currentAppId = m.Groups[1].Value.Trim();
                    if (!target.ContainsKey(currentAppId))
                    {
                        target[currentAppId] = new Dictionary<string, SlsConfigEntry>(StringComparer.OrdinalIgnoreCase);
                    }
                }
            }
            else if (line.StartsWith("    ") && currentAppId != null)
            {
                var m = MapEntryRegex.Match(line);
                if (m.Success)
                {
                    string dlcId = m.Groups[1].Value.Trim();
                    string dlcName = m.Groups[2].Value.Trim().Trim('"', '\'');
                    string? comment = m.Groups[3].Success ? m.Groups[3].Value.Trim() : null;
                    target[currentAppId][dlcId] = new SlsConfigEntry(dlcId, $"\"{dlcName}\"", comment);
                }
            }
        }
    }

    private static void ParseDenuvoGames(Dictionary<string, List<SlsConfigEntry>> target, string[] lines)
    {
        string? currentSteamId = null;
        foreach (string line in lines)
        {
            string trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#")) continue;

            if (line.StartsWith("  ") && !line.StartsWith("    "))
            {
                var m = NestedKeyRegex.Match(line);
                if (m.Success)
                {
                    currentSteamId = NormalizeSteamId(m.Groups[1].Value.Trim());
                    if (!target.ContainsKey(currentSteamId))
                    {
                        target[currentSteamId] = new List<SlsConfigEntry>();
                    }
                }
            }
            else if (line.StartsWith("    ") && currentSteamId != null)
            {
                var m = ListItemRegex.Match(line);
                if (m.Success)
                {
                    string app = m.Groups[1].Value.Trim();
                    string? comment = m.Groups[2].Success ? m.Groups[2].Value.Trim() : null;
                    target[currentSteamId].Add(new SlsConfigEntry(null, app, comment));
                }
            }
        }
    }

    private static void DeduplicateModel(SlsConfigModel model)
    {
        // Deduplicate AdditionalApps (last occurrence wins)
        var seenApps = new Dictionary<string, SlsConfigEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in model.AdditionalApps)
        {
            string cleanId = entry.Value.Split('#')[0].Trim();
            seenApps[cleanId] = entry;
        }
        model.AdditionalApps = seenApps.Values.ToList();
    }

    public static void EnsurePrerequisites(SlsConfigModel model)
    {
        // 1. Ensure API: yes
        model.Api = true;

        // 2. Ensure LogLevels includes Once (0x2)
        int currentLevels = 0;
        if (!string.IsNullOrWhiteSpace(model.LogLevels))
        {
            string hex = model.LogLevels.Trim();
            if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                int.TryParse(hex[2..], System.Globalization.NumberStyles.HexNumber, null, out currentLevels);
            }
            else
            {
                int.TryParse(hex, out currentLevels);
            }
        }

        if ((currentLevels & 0x2) == 0)
        {
            int newLevels = currentLevels | 0x2;
            model.LogLevels = $"0x{newLevels:X}";
        }
    }

    private static void MergeMap(Dictionary<string, SlsConfigEntry> target, Dictionary<string, SlsConfigEntry> source)
    {
        foreach (var (k, v) in source)
        {
            target[k] = v;
        }
    }

    private static bool ParseYamlBool(string val, bool defaultVal)
    {
        if (string.IsNullOrWhiteSpace(val)) return defaultVal;
        string v = val.Trim().ToLowerInvariant();
        return v is "yes" or "true" or "1" or "on";
    }

    /// <summary>
    /// Serializes SlsConfigModel into formatted YAML preserving clean indentation and comments.
    /// </summary>
    public static string Serialize(SlsConfigModel model)
    {
        var sb = new StringBuilder();

        sb.AppendLine("#Disables Family Share license locking for self and others");
        sb.AppendLine($"DisableFamilyShareLock: {(model.DisableFamilyShareLock ? "yes" : "no")}");
        sb.AppendLine();
        sb.AppendLine("#Switches to whitelist instead of the default blacklist");
        sb.AppendLine($"UseWhitelist: {(model.UseWhitelist ? "yes" : "no")}");
        sb.AppendLine();
        sb.AppendLine("#List of AppIds to ex-/include.");
        sb.AppendLine("AppIds:");
        foreach (var entry in model.AppIds)
        {
            sb.AppendLine($"  - {entry.FormattedValue}");
        }
        sb.AppendLine();
        sb.AppendLine("#Additional AppIds to inject");
        sb.AppendLine("AdditionalApps:");
        foreach (var entry in model.AdditionalApps)
        {
            sb.AppendLine($"  - {entry.FormattedValue}");
        }
        sb.AppendLine();
        sb.AppendLine("#Specific DLC AppIds and names to unlock");
        sb.AppendLine("DlcData:");
        foreach (var (appId, dlcs) in model.DlcData)
        {
            sb.AppendLine($"  {appId}:");
            foreach (var (dlcId, dlcEntry) in dlcs)
            {
                sb.AppendLine($"    {dlcId}: {dlcEntry.FormattedValue}");
            }
        }
        sb.AppendLine();
        sb.AppendLine("#Overrides for product and depot decryption tokens");
        sb.AppendLine("AppTokens:");
        foreach (var (appId, tokenEntry) in model.AppTokens)
        {
            sb.AppendLine($"  {appId}: {tokenEntry.FormattedValue}");
        }
        sb.AppendLine();
        sb.AppendLine("#Fake AppIds for online multiplayer spoofing");
        sb.AppendLine("FakeAppIds:");
        foreach (var (appId, fakeEntry) in model.FakeAppIds)
        {
            sb.AppendLine($"  {appId}: {fakeEntry.FormattedValue}");
        }
        sb.AppendLine();
        sb.AppendLine("#Override Depot manifest IDs");
        sb.AppendLine("ManifestIds:");
        foreach (var (depotId, mEntry) in model.ManifestIds)
        {
            sb.AppendLine($"  {depotId}: {mEntry.FormattedValue}");
        }
        sb.AppendLine();
        sb.AppendLine("#Never download these depots");
        sb.AppendLine("DepotBlacklist:");
        foreach (var entry in model.DepotBlacklist)
        {
            sb.AppendLine($"  - {entry.FormattedValue}");
        }
        sb.AppendLine();
        sb.AppendLine("#Custom ingame statuses");
        sb.AppendLine("IdleStatus:");
        foreach (var (k, v) in model.IdleStatus)
        {
            sb.AppendLine($"  {k}: {v}");
        }
        sb.AppendLine();
        sb.AppendLine("#Override game titles");
        sb.AppendLine("GameTitles:");
        foreach (var (appId, gEntry) in model.GameTitles)
        {
            sb.AppendLine($"  {appId}: {gEntry.FormattedValue}");
        }
        sb.AppendLine();
        sb.AppendLine("#Override purchase timestamps");
        sb.AppendLine("SubscriptionTimestamps:");
        foreach (var (appId, sEntry) in model.SubscriptionTimestamps)
        {
            sb.AppendLine($"  {appId}: {sEntry.FormattedValue}");
        }
        sb.AppendLine();
        sb.AppendLine("#Blocks games from unlocking on wrong accounts");
        sb.AppendLine("DenuvoGames:");
        foreach (var (steamId, games) in model.DenuvoGames)
        {
            sb.AppendLine($"  {steamId}:");
            foreach (var g in games)
            {
                sb.AppendLine($"    - {g.FormattedValue}");
            }
        }
        sb.AppendLine();
        sb.AppendLine("#Overrides your SteamId an app sees");
        sb.AppendLine("SteamIdOverride:");
        foreach (var (appId, sEntry) in model.SteamIdOverride)
        {
            sb.AppendLine($"  {appId}: {sEntry.FormattedValue}");
        }
        sb.AppendLine();
        sb.AppendLine($"MaxSchemaTries: {model.MaxSchemaTries}");
        sb.AppendLine($"SafeMode: {(model.SafeMode ? "yes" : "no")}");
        sb.AppendLine($"WarnHashMissmatch: {(model.WarnHashMissmatch ? "yes" : "no")}");
        sb.AppendLine($"NotifyInit: {(model.NotifyInit ? "yes" : "no")}");
        sb.AppendLine($"API: {(model.Api ? "yes" : "no")}");
        sb.AppendLine($"DisableCloud: {(model.DisableCloud ? "yes" : "no")}");
        sb.AppendLine($"DisableUpdates: {(model.DisableUpdates ? "yes" : "no")}");
        sb.AppendLine($"FakeName: \"{model.FakeName}\"");
        sb.AppendLine($"FakeEmail: \"{model.FakeEmail}\"");
        sb.AppendLine($"FakeWalletBalance: {model.FakeWalletBalance}");
        sb.AppendLine($"LogLevels: {model.LogLevels}");
        sb.AppendLine($"DumpClientInterfaces: {(model.DumpClientInterfaces ? "yes" : "no")}");
        sb.AppendLine($"ExtendedLogging: {(model.ExtendedLogging ? "yes" : "no")}");

        return sb.ToString();
    }

    /// <summary>
    /// Writes content to disk in-place while maintaining the existing file descriptor and triggering inotify watchers.
    /// </summary>
    public static async Task AtomicWriteInPlaceAsync(string filePath, string content, CancellationToken cancellationToken = default)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(content);
        string? dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        await using var stream = new FileStream(
            filePath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.ReadWrite,
            bufferSize: 4096,
            useAsync: true);

        await stream.WriteAsync(bytes, cancellationToken);
        await stream.FlushAsync(cancellationToken);
        stream.Flush(flushToDisk: true); // POSIX fsync equivalent
    }
}
