using Potato.Domain.Models;
using Potato.Domain.ValueObjects;
using Potato.Domain.Vdf;

namespace Potato.Domain.Acf;

/// <summary>
/// Service for parsing, serializing, loading, and saving Steam appmanifest (ACF) files.
/// </summary>
public static class AcfManager
{
    private static readonly HashSet<string> StandardFieldNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "appid",
        "Universe",
        "name",
        "StateFlags",
        "installdir",
        "LastUpdated",
        "LastPlayed",
        "SizeOnDisk",
        "StagingSize",
        "buildid",
        "LastOwner",
        "DownloadType",
        "UpdateResult",
        "BytesToDownload",
        "BytesDownloaded",
        "BytesToStage",
        "BytesStaged",
        "TargetBuildID",
        "AutoUpdateBehavior",
        "AllowOtherDownloadsWhileRunning",
        "ScheduledAutoUpdate",
        "InstalledDepots",
        "InstallScripts",
        "UserConfig",
        "MountedConfig"
    };

    public static AcfAppState LoadFromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"ACF file not found at: {filePath}", filePath);
        }

        string text = File.ReadAllText(filePath);
        return Parse(text);
    }

    public static void SaveToFile(AcfAppState appState, string filePath)
    {
        if (appState == null) throw new ArgumentNullException(nameof(appState));
        if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("File path cannot be empty.", nameof(filePath));

        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        string serialized = Serialize(appState);
        File.WriteAllText(filePath, serialized);
    }

    public static AcfAppState Parse(string vdfText)
    {
        if (string.IsNullOrWhiteSpace(vdfText))
        {
            throw new ArgumentException("VDF text cannot be empty.", nameof(vdfText));
        }

        var root = VdfParser.Parse(vdfText);

        VdfObject appStateObj;
        if (root.TryGetObject("AppState", out var foundObj))
        {
            appStateObj = foundObj;
        }
        else
        {
            appStateObj = root;
        }

        var state = new AcfAppState();

        // 1. Core Scalars
        if (AppId.TryParse(appStateObj.GetString("appid"), out var appId))
        {
            state.AppId = appId;
        }

        state.Universe = appStateObj.GetInt32("Universe", 1);
        state.Name = appStateObj.GetString("name");
        state.StateFlags = appStateObj.GetInt32("StateFlags", 4);
        state.InstallDir = appStateObj.GetString("installdir");
        state.LastUpdated = appStateObj.GetInt64("LastUpdated", 0);
        state.LastPlayed = appStateObj.GetInt64("LastPlayed", 0);
        state.SizeOnDisk = appStateObj.GetUInt64("SizeOnDisk", 0);
        state.StagingSize = appStateObj.GetUInt64("StagingSize", 0);
        state.BuildId = appStateObj.GetString("buildid");
        state.LastOwner = appStateObj.GetUInt64("LastOwner", 0);
        state.DownloadType = appStateObj.GetInt32("DownloadType", 0);
        state.UpdateResult = appStateObj.GetInt32("UpdateResult", 0);
        state.BytesToDownload = appStateObj.GetUInt64("BytesToDownload", 0);
        state.BytesDownloaded = appStateObj.GetUInt64("BytesDownloaded", 0);
        state.BytesToStage = appStateObj.GetUInt64("BytesToStage", 0);
        state.BytesStaged = appStateObj.GetUInt64("BytesStaged", 0);
        state.TargetBuildID = appStateObj.GetString("TargetBuildID");
        state.AutoUpdateBehavior = appStateObj.GetInt32("AutoUpdateBehavior", 0);
        state.AllowOtherDownloadsWhileRunning = appStateObj.GetInt32("AllowOtherDownloadsWhileRunning", 0);
        state.ScheduledAutoUpdate = appStateObj.GetInt32("ScheduledAutoUpdate", 0);

        // 2. InstalledDepots
        if (appStateObj.TryGetObject("InstalledDepots", out var depotsObj))
        {
            foreach (var (depotKey, depotNode) in depotsObj)
            {
                if (DepotId.TryParse(depotKey, out var depotId))
                {
                    ManifestGid manifestGid = ManifestGid.Empty;
                    ulong size = 0;

                    if (depotNode is VdfObject depotDetails)
                    {
                        if (ManifestGid.TryParse(depotDetails.GetString("manifest"), out var gid))
                        {
                            manifestGid = gid;
                        }
                        size = depotDetails.GetUInt64("size", 0);
                    }

                    state.InstalledDepots.Add(new InstalledDepotInfo(depotId, manifestGid, size));
                }
            }
        }

        // 3. InstallScripts
        if (appStateObj.TryGetObject("InstallScripts", out var scriptsObj))
        {
            foreach (var (scriptKey, scriptNode) in scriptsObj)
            {
                if (scriptNode is VdfValue scriptVal)
                {
                    state.InstallScripts[scriptKey] = scriptVal.Value;
                }
            }
        }

        // 4. UserConfig
        if (appStateObj.TryGetObject("UserConfig", out var userCfgObj))
        {
            foreach (var (cfgKey, cfgNode) in userCfgObj)
            {
                if (cfgNode is VdfValue cfgVal)
                {
                    state.UserConfig[cfgKey] = cfgVal.Value;
                }
            }
        }

        // 5. MountedConfig
        if (appStateObj.TryGetObject("MountedConfig", out var mountedCfgObj))
        {
            foreach (var (cfgKey, cfgNode) in mountedCfgObj)
            {
                if (cfgNode is VdfValue cfgVal)
                {
                    state.MountedConfig[cfgKey] = cfgVal.Value;
                }
            }
        }

        // 6. Additional / Custom Fields
        foreach (var (key, node) in appStateObj)
        {
            if (!StandardFieldNames.Contains(key))
            {
                state.AdditionalFields[key] = node;
            }
        }

        return state;
    }

    public static string Serialize(AcfAppState state)
    {
        if (state == null) throw new ArgumentNullException(nameof(state));

        var appStateObj = new VdfObject();

        // 1. Standard Scalars
        appStateObj.Set("appid", state.AppId.ToString());
        appStateObj.Set("Universe", state.Universe.ToString());
        appStateObj.Set("name", state.Name);
        appStateObj.Set("StateFlags", state.StateFlags.ToString());
        appStateObj.Set("installdir", state.InstallDir);
        appStateObj.Set("LastUpdated", state.LastUpdated.ToString());
        appStateObj.Set("LastPlayed", state.LastPlayed.ToString());
        appStateObj.Set("SizeOnDisk", state.SizeOnDisk.ToString());
        appStateObj.Set("StagingSize", state.StagingSize.ToString());
        appStateObj.Set("buildid", state.BuildId);
        appStateObj.Set("LastOwner", state.LastOwner.ToString());
        appStateObj.Set("DownloadType", state.DownloadType.ToString());
        appStateObj.Set("UpdateResult", state.UpdateResult.ToString());
        appStateObj.Set("BytesToDownload", state.BytesToDownload.ToString());
        appStateObj.Set("BytesDownloaded", state.BytesDownloaded.ToString());
        appStateObj.Set("BytesToStage", state.BytesToStage.ToString());
        appStateObj.Set("BytesStaged", state.BytesStaged.ToString());
        appStateObj.Set("TargetBuildID", state.TargetBuildID);
        appStateObj.Set("AutoUpdateBehavior", state.AutoUpdateBehavior.ToString());
        appStateObj.Set("AllowOtherDownloadsWhileRunning", state.AllowOtherDownloadsWhileRunning.ToString());
        appStateObj.Set("ScheduledAutoUpdate", state.ScheduledAutoUpdate.ToString());

        // 2. InstalledDepots
        var depotsObj = new VdfObject();
        foreach (var depot in state.InstalledDepots)
        {
            var depotDetailObj = new VdfObject();
            depotDetailObj.Set("manifest", depot.ManifestGid.ToString());
            depotDetailObj.Set("size", depot.SizeBytes.ToString());
            depotsObj.Set(depot.DepotId.ToString(), depotDetailObj);
        }
        appStateObj.Set("InstalledDepots", depotsObj);

        // 3. InstallScripts (if any)
        if (state.InstallScripts.Count > 0)
        {
            var scriptsObj = new VdfObject();
            foreach (var (k, v) in state.InstallScripts)
            {
                scriptsObj.Set(k, v);
            }
            appStateObj.Set("InstallScripts", scriptsObj);
        }

        // 4. UserConfig
        var userCfgObj = new VdfObject();
        foreach (var (k, v) in state.UserConfig)
        {
            userCfgObj.Set(k, v);
        }
        appStateObj.Set("UserConfig", userCfgObj);

        // 5. MountedConfig
        var mountedCfgObj = new VdfObject();
        foreach (var (k, v) in state.MountedConfig)
        {
            mountedCfgObj.Set(k, v);
        }
        appStateObj.Set("MountedConfig", mountedCfgObj);

        // 6. Additional Fields
        foreach (var (k, node) in state.AdditionalFields)
        {
            appStateObj.Set(k, node);
        }

        return VdfSerializer.Serialize(appStateObj, "AppState");
    }

    public static Game ToGame(AcfAppState state, string branch = "public")
    {
        if (state == null) throw new ArgumentNullException(nameof(state));

        var depots = state.InstalledDepots
            .Select(d => new Depot(d.DepotId, d.ManifestGid, d.SizeBytes))
            .ToList();

        return new Game(
            appId: state.AppId,
            name: state.Name,
            installDir: state.InstallDir,
            buildId: state.BuildId,
            branch: branch,
            installedDepots: depots
        );
    }

    public static AcfAppState FromGame(Game game, ulong lastOwner = 76561199083839651, ulong sizeOnDisk = 0)
    {
        if (game == null) throw new ArgumentNullException(nameof(game));

        var state = new AcfAppState
        {
            AppId = game.AppId,
            Name = game.Name,
            InstallDir = string.IsNullOrWhiteSpace(game.InstallDir) ? game.Name : game.InstallDir,
            BuildId = game.BuildId,
            TargetBuildID = game.BuildId,
            LastOwner = lastOwner,
            SizeOnDisk = sizeOnDisk,
            StateFlags = 4
        };

        if (game.InstalledDepots != null)
        {
            foreach (var depot in game.InstalledDepots)
            {
                state.InstalledDepots.Add(new InstalledDepotInfo(depot.DepotId, depot.ManifestGid, depot.SizeBytes));
            }
        }

        return state;
    }
}
