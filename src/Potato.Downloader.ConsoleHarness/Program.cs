using Potato.Domain.ValueObjects;
using Potato.Downloader.Options;
using Potato.Downloader.Process;
using Potato.Downloader.Progress;
using Potato.ManifestApi.Cache;
using Potato.ManifestApi.Client;
using Potato.ManifestApi.Models;
using Potato.ManifestApi.Quota;
using Potato.Pipeline.Keys;
using Potato.Pipeline.Models;
using Potato.Pipeline.Orchestrator;
using Potato.SlsSteam.Config;
using Potato.SlsSteam.Ipc;
using Potato.SlsSteam.Paths;
using Potato.SteamMetadata.Clients;
using Potato.SteamMetadata.Resolver;
using Potato.SteamMetadata.Storage;

namespace Potato.Downloader.ConsoleHarness;

internal class Program
{
    private static async Task<int> Main(string[] args)
    {
        Console.WriteLine("=================================================");
        Console.WriteLine(" Potato Downloader, Manifest, Metadata & Pipeline");
        Console.WriteLine("=================================================");

        if (args.Length == 0 || args.Contains("--help") || args.Contains("-h"))
        {
            PrintUsage();
            return 0;
        }

        if (args.Contains("--sls-status"))
        {
            return HandleSlsStatus();
        }

        if (args.Contains("--sls-heal"))
        {
            return await HandleSlsHealAsync(args);
        }

        if (args.Contains("--install"))
        {
            return await HandleInstallAsync(args);
        }

        if (args.Contains("--resolve-manifest"))
        {
            return await HandleResolveManifestAsync(args);
        }

        if (args.Contains("--resolve-metadata"))
        {
            return await HandleResolveMetadataAsync(args);
        }

        return await HandleDownloadAsync(args);
    }

    private static int HandleSlsStatus()
    {
        var pathResolver = new SlsSteamPathResolver();
        var ipcClient = new SlsSteamIpcClient(pathResolver);

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("--- SLSsteam Environment Status ---");
        Console.ResetColor();

        Console.WriteLine($"  • Steam Path:       {pathResolver.SteamPath} (Flatpak: {pathResolver.IsFlatpakSteam})");
        Console.WriteLine($"  • Config Path:      {pathResolver.ConfigPath} (Exists: {File.Exists(pathResolver.ConfigPath)})");
        Console.WriteLine($"  • Log Path:         {pathResolver.LogPath} (Exists: {File.Exists(pathResolver.LogPath)})");
        Console.WriteLine($"  • API Pipe:         {pathResolver.ApiPipePath} (Available: {ipcClient.IsPipeAvailable})");
        Console.WriteLine($"  • Process Active:   {ipcClient.IsSlsSteamActive}");
        Console.WriteLine($"  • SteamApps Count:  {pathResolver.SteamAppsPaths.Count}");
        foreach (var p in pathResolver.SteamAppsPaths)
        {
            Console.WriteLine($"    - {p}");
        }

        return 0;
    }

    private static async Task<int> HandleSlsHealAsync(string[] args)
    {
        string? configPath = null;
        bool dryRun = false;

        for (int i = 0; i < args.Length; i++)
        {
            if ((args[i] == "--config" || args[i] == "-config") && i + 1 < args.Length)
            {
                configPath = Path.GetFullPath(args[i + 1]);
                i++;
            }
            else if (args[i] == "--dry-run")
            {
                dryRun = true;
            }
        }

        var pathResolver = new SlsSteamPathResolver(explicitConfigPath: configPath);
        string targetPath = pathResolver.ConfigPath;

        if (!File.Exists(targetPath))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error: Config file not found at {targetPath}");
            Console.ResetColor();
            return 1;
        }

        Console.WriteLine($"Reading and Healing SLSsteam Config: {targetPath} (DryRun: {dryRun})...");
        string originalYaml = await File.ReadAllTextAsync(targetPath);
        var model = SlsConfigHealer.ParseAndHeal(originalYaml);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("\n✓ YAML Successfully Parsed and Healed:");
        Console.ResetColor();
        Console.WriteLine($"  • AdditionalApps:         {model.AdditionalApps.Count} app(s)");
        Console.WriteLine($"  • AppTokens:              {model.AppTokens.Count} token(s)");
        Console.WriteLine($"  • FakeAppIds:             {model.FakeAppIds.Count} entry(ies)");
        Console.WriteLine($"  • DlcData Apps:           {model.DlcData.Count} app(s)");
        Console.WriteLine($"  • DenuvoGames Accounts:   {model.DenuvoGames.Count} account(s)");
        Console.WriteLine($"  • API Enabled:            {model.Api}");
        Console.WriteLine($"  • LogLevels:              {model.LogLevels}");

        if (!dryRun)
        {
            var manager = new SlsConfigManager(pathResolver);
            await manager.SaveAsync(model, targetPath);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n✓ Backup saved and healed config written in-place to {targetPath}");
            Console.ResetColor();
        }
        else
        {
            Console.WriteLine("\n[DRY RUN] No changes were written to disk.");
        }

        return 0;
    }

    private static async Task<int> HandleInstallAsync(string[] args)
    {
        AppId appId = AppId.Empty;
        string? destinationDir = null;
        string branch = "public";
        var selectedDepots = new List<DepotId>();
        int maxDownloads = 4;
        bool validate = true;
        bool unlockSls = false;
        string? apiKey = Environment.GetEnvironmentVariable("HUBCAP_API_KEY");

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            string next = (i + 1 < args.Length) ? args[i + 1] : string.Empty;

            switch (arg)
            {
                case "--app":
                case "-app":
                    appId = AppId.Parse(next);
                    i++;
                    break;
                case "--dir":
                case "-dir":
                    destinationDir = Path.GetFullPath(next);
                    i++;
                    break;
                case "--branch":
                case "-branch":
                    branch = next;
                    i++;
                    break;
                case "--depot":
                case "-depot":
                    if (DepotId.TryParse(next, out var dId)) selectedDepots.Add(dId);
                    i++;
                    break;
                case "--max-downloads":
                case "-max-downloads":
                    if (int.TryParse(next, out int maxDl)) maxDownloads = maxDl;
                    i++;
                    break;
                case "--validate":
                case "-validate":
                    validate = true;
                    break;
                case "--no-validate":
                case "-no-validate":
                    validate = false;
                    break;
                case "--unlock-sls":
                case "-unlock-sls":
                    unlockSls = true;
                    break;
                case "--api-key":
                case "-api-key":
                    apiKey = next;
                    i++;
                    break;
            }
        }

        if (!appId.IsValid)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Error: --app <appid> is required for installation.");
            Console.ResetColor();
            return 1;
        }

        if (string.IsNullOrWhiteSpace(destinationDir))
        {
            string baseDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            destinationDir = Path.Combine(baseDir, ".local", "share", "Steam");
        }

        Console.WriteLine($"Starting Full Pipeline Installation for AppID: {appId}");
        Console.WriteLine($"Destination Library: {destinationDir}");
        Console.WriteLine($"Branch:              {branch}");
        Console.WriteLine($"Max Downloads:       {maxDownloads}");
        Console.WriteLine($"Validate:            {validate}");
        Console.WriteLine($"Unlock SLSsteam:     {unlockSls}");
        Console.WriteLine();

        using var store = new SqliteSteamMetadataStore();
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        var steamCmdClient = new SteamCmdRestClient(httpClient);
        var storeWebClient = new SteamStoreWebClient(httpClient);
        using var picsClient = new SteamPicsClient();
        var metadataResolver = new SteamMetadataResolver(store, steamCmdClient, picsClient, storeWebClient);

        var cacheStore = new FileManifestCacheStore();
        var quotaTracker = new QuotaTracker();
        var manifestOptions = new HubcapApiOptions { ApiKey = apiKey };
        var manifestClient = new HubcapApiClient(httpClient, cacheStore, quotaTracker, manifestOptions);

        using var depotKeyStore = new SqliteDepotKeyStore();
        var pathResolver = new SlsSteamPathResolver();
        var slsConfigManager = new SlsConfigManager(pathResolver);
        var slsIpcClient = new SlsSteamIpcClient(pathResolver);

        var orchestrator = new InstallGameOrchestrator(
            metadataResolver,
            manifestClient,
            depotKeyStore,
            () => new DepotDownloaderProcess(),
            slsConfigManager,
            slsIpcClient,
            pathResolver);

        var request = new InstallRequest(
            appId,
            destinationDir,
            branch,
            selectedDepots.Count > 0 ? selectedDepots : null,
            maxDownloads,
            validate,
            useLanCache: false,
            unlockSls: unlockSls);

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            Console.WriteLine("\n[CANCEL] Installation cancel requested...");
            cts.Cancel();
        };

        var progress = new Progress<InstallProgressReport>(report =>
        {
            if (report.DownloadProgress != null)
            {
                var dp = report.DownloadProgress;
                if (dp.IsValidating)
                {
                    Console.Write($"\r[VALIDATING] {dp.RawLine.PadRight(80)}");
                }
                else
                {
                    int barWidth = 25;
                    int filled = (int)Math.Round(dp.Percentage / 100.0 * barWidth);
                    string bar = new string('█', filled) + new string('░', barWidth - filled);
                    Console.Write($"\r[{bar}] {dp.Percentage,6:F2}% | {dp.FormattedSpeed,10} | ETA: {dp.FormattedEta,-15}");
                }
            }
            else
            {
                Console.WriteLine($"[{report.Step.ToString().ToUpperInvariant()}] {report.Message}");
            }
        });

        try
        {
            var result = await orchestrator.InstallGameAsync(request, progress, cts.Token);
            Console.WriteLine();

            if (result.Success)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("=================================================");
                Console.WriteLine($"✓ GAME SUCCESSFULLY INSTALLED: {result.GameName}");
                Console.WriteLine("=================================================");
                Console.ResetColor();
                Console.WriteLine($"  • AppID:         {result.AppId}");
                Console.WriteLine($"  • Install Dir:   {result.InstallDir}");
                Console.WriteLine($"  • Size on Disk:  {result.TotalBytesOnDisk:N0} bytes");
                Console.WriteLine($"  • Manifest ACF:  {result.AcfPath}");
                Console.WriteLine();
                return 0;
            }

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n✗ Installation failed: {result.ErrorMessage}");
            Console.ResetColor();
            return 1;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\nInstallation error: {ex.Message}");
            Console.ResetColor();
            return 1;
        }
    }

    private static async Task<int> HandleResolveMetadataAsync(string[] args)
    {
        AppId appId = AppId.Empty;
        AppToken? appToken = null;
        bool forceRefresh = false;

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            string next = (i + 1 < args.Length) ? args[i + 1] : string.Empty;

            switch (arg)
            {
                case "--app":
                case "-app":
                    appId = AppId.Parse(next);
                    i++;
                    break;
                case "--token":
                case "-token":
                    if (AppToken.TryParse(next, out var token))
                    {
                        appToken = token;
                    }
                    i++;
                    break;
                case "--force-refresh":
                case "-force-refresh":
                    forceRefresh = true;
                    break;
            }
        }

        if (!appId.IsValid)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Error: --app <appid> is required for metadata resolution.");
            Console.ResetColor();
            return 1;
        }

        Console.WriteLine($"Resolving metadata for AppID {appId} (ForceRefresh: {forceRefresh})...");
        using var store = new SqliteSteamMetadataStore();
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        var steamCmdClient = new SteamCmdRestClient(httpClient);
        var storeWebClient = new SteamStoreWebClient(httpClient);
        using var picsClient = new SteamPicsClient();
        var resolver = new SteamMetadataResolver(store, steamCmdClient, picsClient, storeWebClient);

        try
        {
            var metadata = await resolver.ResolveAppMetadataAsync(appId, appToken, forceRefresh);
            if (metadata == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("✗ Failed to resolve metadata across all 4 layers.");
                Console.ResetColor();
                return 1;
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n✓ Resolved via Layer: {metadata.Source.ToUpperInvariant()}");
            Console.ResetColor();
            Console.WriteLine($"  • App Name:    {metadata.Name}");
            Console.WriteLine($"  • Install Dir: {metadata.InstallDir}");
            Console.WriteLine($"  • Build ID:    {metadata.BuildId ?? "N/A"}");
            Console.WriteLine($"  • Header URL:  {metadata.HeaderUrl ?? "N/A"}");
            Console.WriteLine($"  • Depots ({metadata.Depots.Count}):");
            foreach (var (depotId, dInfo) in metadata.Depots)
            {
                string sizeStr = !string.IsNullOrEmpty(dInfo.Size) ? $" | Size: {dInfo.Size}" : "";
                string gidStr = dInfo.ManifestGid != null ? $" | GID: {dInfo.ManifestGid}" : "";
                Console.WriteLine($"    - Depot {depotId}: '{dInfo.Name}'{gidStr}{sizeStr}");
            }

            // Demonstrate second-call DB cache roundtrip
            Console.WriteLine("\n--- Testing Round-Trip DB Cache Read ---");
            var cached = await store.GetAppInfoAsync(appId);
            if (cached != null)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"✓ SQLite Cache Hit Verified! Found '{cached.Name}' ({cached.Depots.Count} depots) stored in DB.");
                Console.ResetColor();
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\nError resolving metadata: {ex.Message}");
            Console.ResetColor();
            return 1;
        }
    }

    private static async Task<int> HandleResolveManifestAsync(string[] args)
    {
        AppId appId = AppId.Empty;
        DepotId depotId = DepotId.Empty;
        ManifestGid manifestGid = ManifestGid.Empty;
        string branch = "public";
        string? apiKey = Environment.GetEnvironmentVariable("HUBCAP_API_KEY");

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            string next = (i + 1 < args.Length) ? args[i + 1] : string.Empty;

            switch (arg)
            {
                case "--app":
                case "-app":
                    appId = AppId.Parse(next);
                    i++;
                    break;
                case "--depot":
                case "-depot":
                    depotId = DepotId.Parse(next);
                    i++;
                    break;
                case "--manifest":
                case "-manifest":
                    manifestGid = ManifestGid.Parse(next);
                    i++;
                    break;
                case "--branch":
                case "-branch":
                    branch = next;
                    i++;
                    break;
                case "--api-key":
                case "-api-key":
                    apiKey = next;
                    i++;
                    break;
            }
        }

        if (!appId.IsValid)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Error: --app <appid> is required for manifest resolution.");
            Console.ResetColor();
            return 1;
        }

        var requiredDepots = new Dictionary<DepotId, ManifestGid>();
        if (depotId.IsValid && manifestGid.IsValid)
        {
            requiredDepots[depotId] = manifestGid;
        }

        Console.WriteLine($"Resolving manifests for AppID {appId} (Branch: {branch})...");
        if (requiredDepots.Count > 0)
        {
            Console.WriteLine($"Target Depot: {depotId} -> GID: {manifestGid}");
        }
        else
        {
            Console.WriteLine("Target: All app depots via bundle / classic endpoints.");
        }

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            Console.WriteLine("API Key: [CONFIGURED]");
        }
        else
        {
            Console.WriteLine("API Key: [NOT SET - Will check Tier 0 Cache or fail on authenticated tiers]");
        }

        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        var cacheStore = new FileManifestCacheStore();
        var quotaTracker = new QuotaTracker();
        var options = new HubcapApiOptions { ApiKey = apiKey };
        var client = new HubcapApiClient(httpClient, cacheStore, quotaTracker, options);

        try
        {
            var result = await client.ResolveManifestAsync(appId, branch, requiredDepots);

            if (result.Success)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"\n✓ Successfully resolved manifest using {result.TierUsed}!");
                Console.ResetColor();
                Console.WriteLine($"Fetched {result.Manifests.Count} manifest file(s):");
                foreach (var manifest in result.Manifests)
                {
                    Console.WriteLine($"  • {manifest.FileName} ({manifest.Content.Length:N0} bytes)");
                }

                var snapshot = quotaTracker.GetSnapshot();
                Console.WriteLine($"\nQuota Pool Usage Today:");
                Console.WriteLine($"  • Tier 1 Single Manifest: {snapshot.SingleManifestCalls} call(s)");
                Console.WriteLine($"  • Tier 2 Bundle Manifest: {snapshot.BundleManifestCalls} call(s)");
                Console.WriteLine($"  • Tier 3 Classic Zip:     {snapshot.ClassicZipCalls} call(s)");

                return 0;
            }

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n✗ Failed to resolve manifest: {result.ErrorMessage}");
            Console.ResetColor();
            return 1;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\nError resolving manifest: {ex.Message}");
            Console.ResetColor();
            return 1;
        }
    }

    private static async Task<int> HandleDownloadAsync(string[] args)
    {
        var options = new DepotDownloaderOptions();

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            string next = (i + 1 < args.Length) ? args[i + 1] : string.Empty;

            switch (arg)
            {
                case "--app":
                case "-app":
                    options.AppId = AppId.Parse(next);
                    i++;
                    break;
                case "--depot":
                case "-depot":
                    options.DepotId = DepotId.Parse(next);
                    i++;
                    break;
                case "--manifest":
                case "-manifest":
                    options.ManifestGid = ManifestGid.Parse(next);
                    i++;
                    break;
                case "--manifestfile":
                case "-manifestfile":
                    options.ManifestFilePath = Path.GetFullPath(next);
                    i++;
                    break;
                case "--depotkeys":
                case "-depotkeys":
                    options.DepotKeysFilePath = Path.GetFullPath(next);
                    i++;
                    break;
                case "--dir":
                case "-dir":
                    options.DownloadDir = Path.GetFullPath(next);
                    i++;
                    break;
                case "--max-downloads":
                case "-max-downloads":
                    if (int.TryParse(next, out int maxDl)) options.MaxDownloads = maxDl;
                    i++;
                    break;
                case "--branch":
                case "-branch":
                    options.Branch = next;
                    i++;
                    break;
                case "--use-lancache":
                case "-use-lancache":
                    options.UseLanCache = true;
                    break;
                case "--validate":
                case "-validate":
                    options.Validate = true;
                    break;
                case "--no-validate":
                case "-no-validate":
                    options.Validate = false;
                    break;
                case "--filelist":
                case "-filelist":
                    options.FileListPath = next;
                    i++;
                    break;
            }
        }

        if (!options.AppId.IsValid || !options.DepotId.IsValid)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Error: --app and --depot are required.");
            Console.ResetColor();
            PrintUsage();
            return 1;
        }

        if (string.IsNullOrWhiteSpace(options.DownloadDir))
        {
            options.DownloadDir = Path.Combine(Directory.GetCurrentDirectory(), "downloads", options.AppId.ToString());
        }

        Console.WriteLine($"AppID:         {options.AppId}");
        Console.WriteLine($"DepotID:       {options.DepotId}");
        Console.WriteLine($"ManifestGID:   {options.ManifestGid}");
        Console.WriteLine($"Manifest File: {options.ManifestFilePath}");
        Console.WriteLine($"Keys File:     {options.DepotKeysFilePath}");
        Console.WriteLine($"Download Dir:  {options.DownloadDir}");
        Console.WriteLine($"Branch:        {options.Branch}");
        Console.WriteLine($"Max Downloads: {options.MaxDownloads}");
        Console.WriteLine($"Validate:      {options.Validate}");
        Console.WriteLine();
        Console.WriteLine("Controls: [P] Pause | [R] Resume | [Q / Ctrl+C] Stop");
        Console.WriteLine("-------------------------------------------------");

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            Console.WriteLine("\nStopping download...");
            cts.Cancel();
        };

        using var downloader = new DepotDownloaderProcess();

        // Keyboard listener task for pause / resume
        _ = Task.Run(async () =>
        {
            while (!cts.IsCancellationRequested)
            {
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(intercept: true);
                    if (key.Key == ConsoleKey.P)
                    {
                        if (downloader.Pause())
                        {
                            Console.WriteLine("\n[PAUSED] Process tree suspended.");
                        }
                    }
                    else if (key.Key == ConsoleKey.R)
                    {
                        if (downloader.Resume())
                        {
                            Console.WriteLine("\n[RESUMED] Process tree resumed.");
                        }
                    }
                    else if (key.Key == ConsoleKey.Q)
                    {
                        Console.WriteLine("\n[STOPPING] User requested quit.");
                        cts.Cancel();
                    }
                }
                await Task.Delay(100);
            }
        });

        var progress = new Progress<DownloadProgressReport>(report =>
        {
            if (report.IsValidating)
            {
                Console.Write($"\r[VALIDATING] {report.RawLine.PadRight(80)}");
            }
            else
            {
                int barWidth = 25;
                int filled = (int)Math.Round(report.Percentage / 100.0 * barWidth);
                string bar = new string('█', filled) + new string('░', barWidth - filled);

                string speed = report.FormattedSpeed;
                string eta = report.FormattedEta;
                Console.Write($"\r[{bar}] {report.Percentage,6:F2}% | {speed,10} | ETA: {eta,-18}");
            }
        });

        try
        {
            int exitCode = await downloader.RunAsync(options, progress, cts.Token);
            Console.WriteLine();
            if (exitCode == 0)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("✓ Download completed successfully!");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"DepotDownloader exited with code {exitCode}.");
                Console.ResetColor();
            }

            return exitCode;
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("\nDownload was cancelled.");
            return 130;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\nDownload failed: {ex.Message}");
            Console.ResetColor();
            return 1;
        }
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  SLSsteam Status (Step 6):");
        Console.WriteLine("    dotnet run --project src/Potato.Downloader.ConsoleHarness -- --sls-status");
        Console.WriteLine();
        Console.WriteLine("  SLSsteam Config Healing (Step 6):");
        Console.WriteLine("    dotnet run --project src/Potato.Downloader.ConsoleHarness -- --sls-heal [--config <path>] [--dry-run]");
        Console.WriteLine();
        Console.WriteLine("  Install Game (Full Pipeline + Optional SLSsteam Unlock):");
        Console.WriteLine("    dotnet run --project src/Potato.Downloader.ConsoleHarness -- --install --app <appid> [--dir <library_path>] [--branch <name>] [--depot <depotid>] [--unlock-sls]");
        Console.WriteLine();
        Console.WriteLine("  Resolve Metadata (Step 4):");
        Console.WriteLine("    dotnet run --project src/Potato.Downloader.ConsoleHarness -- --resolve-metadata --app <appid> [--token <token>] [--force-refresh]");
        Console.WriteLine();
        Console.WriteLine("  Resolve Manifest (Step 3):");
        Console.WriteLine("    dotnet run --project src/Potato.Downloader.ConsoleHarness -- --resolve-manifest --app <appid> [--depot <depot>] [--manifest <gid>] [--branch <name>]");
        Console.WriteLine();
        Console.WriteLine("  Download Single Depot (Step 2):");
        Console.WriteLine("    dotnet run --project src/Potato.Downloader.ConsoleHarness -- [download-options]");
        Console.WriteLine();
    }
}
