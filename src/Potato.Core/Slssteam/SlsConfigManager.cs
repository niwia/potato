using System.Text.RegularExpressions;
using YamlDotNet.RepresentationModel;

namespace Potato.Core.Slssteam;

public class SlsConfigManager
{
    private static readonly string FlatpakSteamDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".var", "app", "com.valvesoftware.Steam", ".steam", "steam");

    private static readonly string FlatpakConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".var", "app", "com.valvesoftware.Steam", ".config", "SLSsteam", "config.yaml");

    private static readonly string NativeConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".config", "SLSsteam", "config.yaml");

    public static string GetDefaultConfigPath(string? customPath = null)
    {
        if (!string.IsNullOrEmpty(customPath) && File.Exists(customPath))
        {
            return customPath;
        }

        if (Directory.Exists(FlatpakSteamDir))
        {
            return FlatpakConfigPath;
        }

        return NativeConfigPath;
    }

    public static bool EnsureBackup(string configPath)
    {
        try
        {
            if (File.Exists(configPath))
            {
                var bak = configPath + ".bak";
                File.Copy(configPath, bak, overwrite: true);
                return true;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to create config backup: {ex.Message}");
        }
        return false;
    }

    public static bool AddAdditionalApp(string configPath, uint appId, string? comment = null)
    {
        EnsureBackup(configPath);

        if (!File.Exists(configPath))
        {
            var dir = Path.GetDirectoryName(configPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(configPath, "AdditionalApps:\n");
        }

        var lines = File.ReadAllLines(configPath).ToList();
        var appIdStr = appId.ToString();

        // Check if already present
        bool inAdditionalApps = false;
        int insertIndex = -1;

        for (int i = 0; i < lines.Count; i++)
        {
            var trimmed = lines[i].Trim();
            if (trimmed.StartsWith("AdditionalApps:", StringComparison.OrdinalIgnoreCase))
            {
                inAdditionalApps = true;
                insertIndex = i + 1;
                continue;
            }

            if (inAdditionalApps)
            {
                if (trimmed.StartsWith("-") && Regex.IsMatch(trimmed, $@"\b{appIdStr}\b"))
                {
                    // Already exists
                    return true;
                }

                // If next root key starts
                if (!string.IsNullOrWhiteSpace(lines[i]) && !lines[i].StartsWith(" ") && !lines[i].StartsWith("\t") && !trimmed.StartsWith("#"))
                {
                    insertIndex = i;
                    break;
                }
                insertIndex = i + 1;
            }
        }

        var entry = string.IsNullOrWhiteSpace(comment) ? $"  - {appId}" : $"  - {appId} # {comment}";

        if (insertIndex >= 0 && insertIndex <= lines.Count)
        {
            lines.Insert(insertIndex, entry);
        }
        else
        {
            lines.Add("AdditionalApps:");
            lines.Add(entry);
        }

        File.WriteAllLines(configPath, lines);
        return true;
    }

    public static bool RemoveAdditionalApp(string configPath, uint appId)
    {
        if (!File.Exists(configPath)) return false;
        EnsureBackup(configPath);

        var lines = File.ReadAllLines(configPath).ToList();
        var appIdStr = appId.ToString();
        bool inAdditionalApps = false;
        bool modified = false;

        for (int i = lines.Count - 1; i >= 0; i--)
        {
            var trimmed = lines[i].Trim();
            if (trimmed.StartsWith("AdditionalApps:", StringComparison.OrdinalIgnoreCase))
            {
                inAdditionalApps = true;
                continue;
            }

            if (!string.IsNullOrWhiteSpace(lines[i]) && !lines[i].StartsWith(" ") && !lines[i].StartsWith("\t") && !trimmed.StartsWith("#"))
            {
                inAdditionalApps = false;
            }

            if (trimmed.StartsWith("-") && Regex.IsMatch(trimmed, $@"\b{appIdStr}\b"))
            {
                lines.RemoveAt(i);
                modified = true;
            }
        }

        if (modified)
        {
            File.WriteAllLines(configPath, lines);
        }

        return modified;
    }

    public static HashSet<uint> GetAdditionalApps(string configPath)
    {
        var appIds = new HashSet<uint>();
        if (!File.Exists(configPath)) return appIds;

        try
        {
            var text = File.ReadAllText(configPath);
            var yaml = new YamlStream();
            using var reader = new StringReader(text);
            yaml.Load(reader);

            if (yaml.Documents.Count > 0 && yaml.Documents[0].RootNode is YamlMappingNode root)
            {
                if (root.Children.TryGetValue(new YamlScalarNode("AdditionalApps"), out var node) && node is YamlSequenceNode seq)
                {
                    foreach (var item in seq)
                    {
                        if (item is YamlScalarNode scalar && uint.TryParse(scalar.Value, out var id))
                        {
                            appIds.Add(id);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading SLSsteam config: {ex.Message}");
        }

        return appIds;
    }
}
