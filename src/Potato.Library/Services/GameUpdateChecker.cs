using Potato.Domain.ValueObjects;
using Potato.Library.Models;
using Potato.Pipeline.Keys;
using Potato.SteamMetadata.Resolver;

namespace Potato.Library.Services;

/// <summary>
/// Default implementation of IGameUpdateChecker comparing local depot manifests against Steam metadata.
/// </summary>
public sealed class GameUpdateChecker : IGameUpdateChecker
{
    private readonly ISteamMetadataResolver _metadataResolver;
    private readonly IDepotKeyStore _depotKeyStore;

    public GameUpdateChecker(
        ISteamMetadataResolver metadataResolver,
        IDepotKeyStore? depotKeyStore = null)
    {
        _metadataResolver = metadataResolver ?? throw new ArgumentNullException(nameof(metadataResolver));
        _depotKeyStore = depotKeyStore ?? new SqliteDepotKeyStore();
    }

    public async Task<UpdateStatusResult> CheckGameUpdateAsync(
        InstalledGame game,
        string branch = "public",
        CancellationToken cancellationToken = default)
    {
        if (game == null || !game.AppId.IsValid)
        {
            return UpdateStatusResult.CannotDetermine(game?.AppId ?? AppId.Empty, "Invalid game or AppID.");
        }

        game.UpdateStatus = UpdateStatus.Checking;

        try
        {
            var appToken = await _depotKeyStore.GetAppTokenAsync(game.AppId, cancellationToken);
            var metadata = await _metadataResolver.ResolveAppMetadataAsync(game.AppId, appToken, forceRefresh: false, cancellationToken);

            if (metadata == null)
            {
                game.UpdateStatus = UpdateStatus.CannotDetermine;
                return UpdateStatusResult.CannotDetermine(game.AppId, "Could not resolve metadata from Steam API or local cache.");
            }

            var diffs = new List<DepotUpdateDiff>();
            string targetBuildId = !string.IsNullOrWhiteSpace(metadata.BuildId) ? metadata.BuildId : "0";

            foreach (var installedDepot in game.InstalledDepots)
            {
                if (metadata.Depots.TryGetValue(installedDepot.DepotId, out var upstreamDepot))
                {
                    ManifestGid? targetGid = null;
                    if (upstreamDepot.Manifests.TryGetValue(branch, out var bGid))
                    {
                        targetGid = bGid;
                    }
                    else if (upstreamDepot.ManifestGid != null)
                    {
                        targetGid = upstreamDepot.ManifestGid;
                    }

                    if (targetGid != null && targetGid.Value.IsValid)
                    {
                        if (installedDepot.ManifestGid != targetGid.Value)
                        {
                            diffs.Add(new DepotUpdateDiff(
                                installedDepot.DepotId,
                                installedDepot.ManifestGid,
                                targetGid.Value,
                                upstreamDepot.Name));
                        }
                    }
                }
            }

            if (diffs.Count > 0)
            {
                game.UpdateStatus = UpdateStatus.UpdateAvailable;
                game.PendingDepotUpdates = diffs;
                return UpdateStatusResult.UpdateAvailable(game.AppId, game.BuildId, targetBuildId, diffs);
            }

            game.UpdateStatus = UpdateStatus.UpToDate;
            game.PendingDepotUpdates = Array.Empty<DepotUpdateDiff>();
            return UpdateStatusResult.UpToDate(game.AppId, game.BuildId);
        }
        catch (Exception ex)
        {
            game.UpdateStatus = UpdateStatus.CannotDetermine;
            return UpdateStatusResult.CannotDetermine(game.AppId, ex.Message);
        }
    }

    public async Task<IReadOnlyDictionary<AppId, UpdateStatusResult>> CheckAllUpdatesAsync(
        IReadOnlyList<InstalledGame> games,
        string branch = "public",
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<AppId, UpdateStatusResult>();
        int current = 0;

        foreach (var game in games)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await CheckGameUpdateAsync(game, branch, cancellationToken);
            results[game.AppId] = result;

            current++;
            progress?.Report(current);
        }

        return results;
    }
}
