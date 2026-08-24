using System.Text;
using Potato.Domain.Acf;
using Potato.Domain.Models;
using Potato.Domain.ValueObjects;
using Potato.Downloader.Options;
using Potato.Downloader.Process;
using Potato.Downloader.Progress;
using Potato.ManifestApi.Client;
using Potato.Pipeline.Keys;
using Potato.Pipeline.Models;
using Potato.SlsSteam.Config;
using Potato.SlsSteam.Ipc;
using Potato.SlsSteam.Paths;
using Potato.SteamMetadata.Models;
using Potato.SteamMetadata.Resolver;

namespace Potato.Pipeline.Orchestrator;

/// <summary>
/// Implementation of IInstallGameOrchestrator executing the complete 5-stage installation pipeline.
/// </summary>
public sealed class InstallGameOrchestrator : IInstallGameOrchestrator
{
    private readonly ISteamMetadataResolver _metadataResolver;
    private readonly IHubcapApiClient _manifestApiClient;
    private readonly IDepotKeyStore _depotKeyStore;
    private readonly Func<IDepotDownloaderProcess> _processFactory;
    private readonly ISlsConfigManager _slsConfigManager;
    private readonly ISlsSteamIpcClient _slsIpcClient;
    private readonly ISlsSteamPathResolver _slsPathResolver;

    public InstallGameOrchestrator(
        ISteamMetadataResolver metadataResolver,
        IHubcapApiClient manifestApiClient,
        IDepotKeyStore depotKeyStore,
        Func<IDepotDownloaderProcess>? processFactory = null,
        ISlsConfigManager? slsConfigManager = null,
        ISlsSteamIpcClient? slsIpcClient = null,
        ISlsSteamPathResolver? slsPathResolver = null)
    {
        _metadataResolver = metadataResolver ?? throw new ArgumentNullException(nameof(metadataResolver));
        _manifestApiClient = manifestApiClient ?? throw new ArgumentNullException(nameof(manifestApiClient));
        _depotKeyStore = depotKeyStore ?? throw new ArgumentNullException(nameof(depotKeyStore));
        _processFactory = processFactory ?? (() => new DepotDownloaderProcess());
        _slsPathResolver = slsPathResolver ?? new SlsSteamPathResolver();
        _slsConfigManager = slsConfigManager ?? new SlsConfigManager(_slsPathResolver);
        _slsIpcClient = slsIpcClient ?? new SlsSteamIpcClient(_slsPathResolver);
    }

    public async Task<InstallResult> InstallGameAsync(
        InstallRequest request,
        IProgress<InstallProgressReport>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (request == null || !request.AppId.IsValid)
        {
            return InstallResult.CreateFailure(request?.AppId ?? AppId.Empty, "Invalid installation request or AppID.");
        }

        string tempWorkDir = Path.Combine(Path.GetTempPath(), $"potato_install_{request.AppId}_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempWorkDir);

        try
        {
            // ── STAGE 1: RESOLVE METADATA ─────────────────────────────────────
            progress?.Report(new InstallProgressReport(InstallStep.ResolvingMetadata, $"Resolving metadata for AppID {request.AppId}..."));

            var appToken = await _depotKeyStore.GetAppTokenAsync(request.AppId, cancellationToken);
            var metadata = await _metadataResolver.ResolveAppMetadataAsync(request.AppId, appToken, forceRefresh: false, cancellationToken);

            if (metadata == null)
            {
                progress?.Report(new InstallProgressReport(InstallStep.Failed, $"Failed to resolve metadata for AppID {request.AppId}."));
                return InstallResult.CreateFailure(request.AppId, $"Metadata resolution failed for AppID {request.AppId}.");
            }

            string gameName = !string.IsNullOrWhiteSpace(metadata.Name) ? metadata.Name : $"App {request.AppId}";
            string installDir = !string.IsNullOrWhiteSpace(metadata.InstallDir) ? metadata.InstallDir : $"App_{request.AppId}";
            string buildId = !string.IsNullOrWhiteSpace(metadata.BuildId) ? metadata.BuildId : "0";

            progress?.Report(new InstallProgressReport(InstallStep.ResolvingMetadata, $"Metadata resolved: '{gameName}' (InstallDir: {installDir}, BuildID: {buildId})"));

            // Determine target depots
            var targetDepotMap = new Dictionary<DepotId, ManifestGid>();
            foreach (var (depotId, depotInfo) in metadata.Depots)
            {
                if (request.SelectedDepots != null && request.SelectedDepots.Count > 0 && !request.SelectedDepots.Contains(depotId))
                {
                    continue; // Skip depots not in user's selection filter
                }

                // Check manifest GID for requested branch
                ManifestGid? targetGid = null;
                if (depotInfo.Manifests.TryGetValue(request.Branch, out var bGid))
                {
                    targetGid = bGid;
                }
                else if (depotInfo.ManifestGid != null)
                {
                    targetGid = depotInfo.ManifestGid;
                }

                if (targetGid != null && targetGid.Value.IsValid)
                {
                    targetDepotMap[depotId] = targetGid.Value;
                }
            }

            if (targetDepotMap.Count == 0)
            {
                // Fallback: If no manifest GIDs were identified in metadata, try fetching bundle directly for all depots
                foreach (var depotId in metadata.Depots.Keys)
                {
                    if (request.SelectedDepots == null || request.SelectedDepots.Contains(depotId))
                    {
                        targetDepotMap[depotId] = ManifestGid.Empty;
                    }
                }
            }

            // ── STAGE 2: RESOLVE DEPOT KEYS ───────────────────────────────────
            progress?.Report(new InstallProgressReport(InstallStep.ResolvingKeys, $"Checking cached depot decryption keys for {request.AppId}..."));
            var cachedKeys = await _depotKeyStore.GetDepotKeysAsync(request.AppId, cancellationToken);

            // ── STAGE 3: RESOLVE MANIFESTS (4-TIER HUBCAP RESOLUTION) ──────────
            progress?.Report(new InstallProgressReport(InstallStep.ResolvingManifests, $"Resolving manifests via Hubcap API for {targetDepotMap.Count} depot(s)..."));
            var manifestResult = await _manifestApiClient.ResolveManifestAsync(request.AppId, request.Branch, targetDepotMap, cancellationToken);

            if (!manifestResult.Success || manifestResult.Manifests.Count == 0)
            {
                progress?.Report(new InstallProgressReport(InstallStep.Failed, $"Failed to resolve manifests: {manifestResult.ErrorMessage}"));
                return InstallResult.CreateFailure(request.AppId, $"Manifest resolution failed: {manifestResult.ErrorMessage}");
            }

            progress?.Report(new InstallProgressReport(InstallStep.ResolvingManifests, $"Manifests resolved successfully using {manifestResult.TierUsed} ({manifestResult.Manifests.Count} files)."));

            // ── STAGE 4: DOWNLOAD DEPOTS VIA DEPOTDOWNLOADER ──────────────────
            string gameCommonDir = Path.Combine(request.DestinationPath, "steamapps", "common", installDir);
            Directory.CreateDirectory(gameCommonDir);

            // Assemble keys file (depot_id;hex_key)
            string keysFilePath = Path.Combine(tempWorkDir, "keys.vdf");
            var keysSb = new StringBuilder();
            foreach (var (dId, key) in cachedKeys)
            {
                keysSb.AppendLine($"{dId};{key}");
            }
            await File.WriteAllTextAsync(keysFilePath, keysSb.ToString(), cancellationToken);

            // Write decrypted manifest files to temp dir
            var manifestFilePaths = new Dictionary<DepotId, (ManifestGid Gid, string FilePath)>();
            foreach (var mEntry in manifestResult.Manifests)
            {
                string mPath = Path.Combine(tempWorkDir, mEntry.FileName);
                await File.WriteAllBytesAsync(mPath, mEntry.Content, cancellationToken);
                manifestFilePaths[mEntry.DepotId] = (mEntry.ManifestGid, mPath);
            }

            // Download each depot
            int totalDepots = manifestFilePaths.Count;
            int currentDepotIndex = 0;

            foreach (var (depotId, (gid, mPath)) in manifestFilePaths)
            {
                currentDepotIndex++;
                string depotName = metadata.Depots.TryGetValue(depotId, out var dInfo) && !string.IsNullOrWhiteSpace(dInfo.Name)
                    ? dInfo.Name
                    : $"Depot {depotId}";

                progress?.Report(new InstallProgressReport(
                    InstallStep.DownloadingDepots,
                    $"[{currentDepotIndex}/{totalDepots}] Downloading {depotName} ({depotId})..."));

                var dlOptions = new DepotDownloaderOptions
                {
                    AppId = request.AppId,
                    DepotId = depotId,
                    ManifestGid = gid,
                    ManifestFilePath = mPath,
                    DepotKeysFilePath = File.Exists(keysFilePath) && cachedKeys.Count > 0 ? keysFilePath : null,
                    DownloadDir = gameCommonDir,
                    Branch = request.Branch,
                    MaxDownloads = request.MaxDownloads,
                    Validate = request.Validate,
                    UseLanCache = request.UseLanCache
                };

                using var process = _processFactory();
                var dlProgress = new Progress<DownloadProgressReport>(report =>
                {
                    progress?.Report(new InstallProgressReport(
                        InstallStep.DownloadingDepots,
                        $"[{currentDepotIndex}/{totalDepots}] Downloading {depotName}: {report.FormattedSpeed} | ETA: {report.FormattedEta}",
                        report));
                });

                int exitCode = await process.RunAsync(dlOptions, dlProgress, cancellationToken);
                if (exitCode != 0)
                {
                    progress?.Report(new InstallProgressReport(InstallStep.Failed, $"DepotDownloader failed on depot {depotId} with exit code {exitCode}."));
                    return InstallResult.CreateFailure(request.AppId, $"Download failed on Depot {depotId} (Exit code: {exitCode}).");
                }
            }

            // ── STAGE 5: FINALIZE ACF MANIFEST ────────────────────────────────
            progress?.Report(new InstallProgressReport(InstallStep.FinalizingAcf, "Calculating disk usage and writing Steam ACF manifest..."));

            long totalBytesOnDisk = CalculateDirectorySize(gameCommonDir);

            var installedDepotsList = new List<InstalledDepotInfo>();
            foreach (var (depotId, (gid, _)) in manifestFilePaths)
            {
                ulong depotSize = metadata.Depots.TryGetValue(depotId, out var dInfo) && ulong.TryParse(dInfo.Size, out var parsedSize)
                    ? parsedSize
                    : 0;

                installedDepotsList.Add(new InstalledDepotInfo(depotId, gid, depotSize));
            }

            var acfState = new AcfAppState
            {
                AppId = request.AppId,
                Name = gameName,
                InstallDir = installDir,
                BuildId = buildId,
                SizeOnDisk = (ulong)totalBytesOnDisk,
                InstalledDepots = installedDepotsList,
                StateFlags = 4
            };

            string steamappsDir = Path.Combine(request.DestinationPath, "steamapps");
            Directory.CreateDirectory(steamappsDir);
            string acfFilePath = Path.Combine(steamappsDir, $"appmanifest_{request.AppId}.acf");

            AcfManager.SaveToFile(acfState, acfFilePath);

            // ── OPTIONAL SLSSTEAM UNLOCK ──────────────────────────────────────
            if (request.UnlockSls)
            {
                try
                {
                    progress?.Report(new InstallProgressReport(InstallStep.FinalizingAcf, "Registering AppID in SLSsteam config.yaml..."));
                    await _slsConfigManager.AddAdditionalAppAsync(request.AppId, gameName, cancellationToken: cancellationToken);

                    if (appToken != null && appToken.Value.IsValid)
                    {
                        await _slsConfigManager.AddAppTokenAsync(request.AppId, appToken.Value, gameName, cancellationToken: cancellationToken);
                    }

                    if (_slsIpcClient.IsPipeAvailable)
                    {
                        int libIdx = _slsPathResolver.GetLibraryIndex(request.DestinationPath);
                        progress?.Report(new InstallProgressReport(InstallStep.FinalizingAcf, $"Sending install pipe command to SLSsteam (Library Index: {libIdx})..."));
                        await _slsIpcClient.InstallAppAsync(request.AppId, libIdx, cancellationToken);
                    }
                }
                catch (Exception slsEx)
                {
                    progress?.Report(new InstallProgressReport(InstallStep.FinalizingAcf, $"Note: SLSsteam registration notice: {slsEx.Message}"));
                }
            }

            progress?.Report(new InstallProgressReport(
                InstallStep.Completed,
                $"[SUCCESS] Successfully installed '{gameName}' ({totalBytesOnDisk:N0} bytes) to {gameCommonDir}"));

            return InstallResult.CreateSuccess(
                request.AppId,
                gameName,
                installDir,
                acfFilePath,
                totalBytesOnDisk);
        }
        catch (OperationCanceledException)
        {
            progress?.Report(new InstallProgressReport(InstallStep.Failed, "Installation was cancelled."));
            return InstallResult.CreateFailure(request.AppId, "Installation was cancelled.");
        }
        catch (Exception ex)
        {
            progress?.Report(new InstallProgressReport(InstallStep.Failed, $"Installation error: {ex.Message}"));
            return InstallResult.CreateFailure(request.AppId, ex.Message);
        }
        finally
        {
            if (Directory.Exists(tempWorkDir))
            {
                try { Directory.Delete(tempWorkDir, recursive: true); } catch { }
            }
        }
    }

    private static long CalculateDirectorySize(string directoryPath)
    {
        if (!Directory.Exists(directoryPath)) return 0;

        try
        {
            var dirInfo = new DirectoryInfo(directoryPath);
            return dirInfo.EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length);
        }
        catch
        {
            return 0;
        }
    }
}
