using System.Threading.Channels;
using Potato.Domain.ValueObjects;
using Potato.SteamMetadata.Models;
using Potato.SteamMetadata.Storage;
using SteamKit2;

namespace Potato.SteamMetadata.Clients;

/// <summary>
/// Implementation of ISteamPicsClient managing an anonymous SteamClient session in a dedicated background worker.
/// Queries are marshaled to the worker via a channel.
/// </summary>
public sealed class SteamPicsClient : ISteamPicsClient
{
    private sealed record PicsWorkItem(
        AppId AppId,
        AppToken? AppToken,
        TaskCompletionSource<SteamAppMetadata?> CompletionSource,
        CancellationToken CancellationToken);

    private readonly Channel<PicsWorkItem> _channel;
    private readonly Thread _workerThread;
    private readonly CancellationTokenSource _cts = new();

    private SteamClient? _steamClient;
    private CallbackManager? _callbackManager;
    private SteamUser? _steamUser;
    private SteamApps? _steamApps;

    private bool _isConnected;
    private bool _isLoggedOn;
    private bool _disposed;

    public SteamPicsClient()
    {
        _channel = Channel.CreateUnbounded<PicsWorkItem>(new UnboundedChannelOptions { SingleReader = true });
        _workerThread = new Thread(WorkerLoop)
        {
            IsBackground = true,
            Name = "SteamPicsClientWorker"
        };
        _workerThread.Start();
    }

    public async Task<SteamAppMetadata?> FetchProductInfoAsync(
        AppId appId,
        AppToken? appToken = null,
        CancellationToken cancellationToken = default)
    {
        if (!appId.IsValid) return null;

        var tcs = new TaskCompletionSource<SteamAppMetadata?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var workItem = new PicsWorkItem(appId, appToken, tcs, cancellationToken);

        if (!_channel.Writer.TryWrite(workItem))
        {
            return null;
        }

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cts.Token);
        using var reg = linkedCts.Token.Register(() => tcs.TrySetCanceled(linkedCts.Token));

        try
        {
            return await tcs.Task;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch
        {
            return null;
        }
    }

    private void WorkerLoop()
    {
        _steamClient = new SteamClient();
        _callbackManager = new CallbackManager(_steamClient);
        _steamUser = _steamClient.GetHandler<SteamUser>();
        _steamApps = _steamClient.GetHandler<SteamApps>();

        _callbackManager.Subscribe<SteamClient.ConnectedCallback>(OnConnected);
        _callbackManager.Subscribe<SteamClient.DisconnectedCallback>(OnDisconnected);
        _callbackManager.Subscribe<SteamUser.LoggedOnCallback>(OnLoggedOn);

        try
        {
            _steamClient.Connect();
        }
        catch
        {
            // Will retry in loop
        }

        while (!_cts.IsCancellationRequested)
        {
            try
            {
                _callbackManager.RunWaitCallbacks(TimeSpan.FromMilliseconds(50));

                if (!_isConnected)
                {
                    Thread.Sleep(500);
                    if (!_cts.IsCancellationRequested && !_isConnected)
                    {
                        try { _steamClient.Connect(); } catch { }
                    }
                    continue;
                }

                if (!_isLoggedOn)
                {
                    continue;
                }

                // Process pending work items
                while (_channel.Reader.TryRead(out var item))
                {
                    if (item.CancellationToken.IsCancellationRequested)
                    {
                        item.CompletionSource.TrySetCanceled(item.CancellationToken);
                        continue;
                    }

                    ProcessWorkItem(item);
                }
            }
            catch
            {
                // Continue loop
            }
        }

        try
        {
            _steamClient.Disconnect();
        }
        catch
        {
            // Ignore on shutdown
        }
    }

    private void OnConnected(SteamClient.ConnectedCallback callback)
    {
        _isConnected = true;
        _steamUser?.LogOnAnonymous();
    }

    private void OnDisconnected(SteamClient.DisconnectedCallback callback)
    {
        _isConnected = false;
        _isLoggedOn = false;
        if (!_cts.IsCancellationRequested)
        {
            Thread.Sleep(1000);
            try { _steamClient?.Connect(); } catch { }
        }
    }

    private void OnLoggedOn(SteamUser.LoggedOnCallback callback)
    {
        if (callback.Result == EResult.OK)
        {
            _isLoggedOn = true;
        }
        else
        {
            _isLoggedOn = false;
        }
    }

    private void ProcessWorkItem(PicsWorkItem item)
    {
        if (_steamApps == null)
        {
            item.CompletionSource.TrySetResult(null);
            return;
        }

        var request = new SteamApps.PICSRequest((uint)item.AppId.Value);
        if (item.AppToken != null && item.AppToken.Value.IsValid)
        {
            request.AccessToken = item.AppToken.Value.Value;
        }

        var asyncJob = _steamApps.PICSGetProductInfo(new[] { request }, Enumerable.Empty<SteamApps.PICSRequest>());

        // Run callbacks while waiting for job
        var timeoutAt = DateTime.UtcNow.AddSeconds(10);
        bool completed = false;

        asyncJob.ToTask().ContinueWith(task =>
        {
            completed = true;
            if (task.IsFaulted || task.IsCanceled || task.Result == null)
            {
                item.CompletionSource.TrySetResult(null);
                return;
            }

            var resultSet = task.Result;
            if (resultSet.Results == null)
            {
                item.CompletionSource.TrySetResult(null);
                return;
            }

            SteamApps.PICSProductInfoCallback.PICSProductInfo? targetAppInfo = null;
            foreach (var callback in resultSet.Results)
            {
                if (callback.Apps.TryGetValue((uint)item.AppId.Value, out var appInfo))
                {
                    targetAppInfo = appInfo;
                    break;
                }
            }

            if (targetAppInfo == null || targetAppInfo.KeyValues == null)
            {
                item.CompletionSource.TrySetResult(null);
                return;
            }

            var metadata = ParsePicsKeyValues(item.AppId, targetAppInfo.KeyValues);
            item.CompletionSource.TrySetResult(metadata);
        });

        while (!completed && DateTime.UtcNow < timeoutAt && !_cts.IsCancellationRequested)
        {
            _callbackManager?.RunWaitCallbacks(TimeSpan.FromMilliseconds(50));
        }

        if (!completed)
        {
            item.CompletionSource.TrySetResult(null);
        }
    }

    private static SteamAppMetadata ParsePicsKeyValues(AppId appId, KeyValue kv)
    {
        string? appName = kv["common"]["name"].AsString();
        string? installDir = kv["config"]["installdir"].AsString();
        string? headerUrl = SqliteSteamMetadataStore.ConstructFullUrl($"{appId}/header.jpg");

        var depotsKv = kv["depots"];
        var branchesKv = depotsKv["branches"];

        var branches = new Dictionary<string, SteamBranchInfo>();
        string? buildId = null;
        string? timeUpdated = null;

        if (branchesKv != KeyValue.Invalid)
        {
            foreach (var bChild in branchesKv.Children)
            {
                string bName = bChild.Name ?? "public";
                string? bBuildId = bChild["buildid"].AsString();
                string? bTimeUpdated = bChild["timeupdated"].AsString();
                bool pwdReq = bChild["pwdrequired"].AsString() == "1";

                branches[bName] = new SteamBranchInfo(bName, bBuildId, bTimeUpdated, pwdReq);

                if (string.Equals(bName, "public", StringComparison.OrdinalIgnoreCase))
                {
                    buildId = bBuildId;
                    timeUpdated = bTimeUpdated;
                }
            }
        }

        var depots = new Dictionary<DepotId, SteamDepotInfo>();
        if (depotsKv != KeyValue.Invalid)
        {
            foreach (var dChild in depotsKv.Children)
            {
                string dKey = dChild.Name ?? "";
                if (string.Equals(dKey, "branches", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(dKey, "workshopdepots", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(dKey, "branches_public", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!DepotId.TryParse(dKey, out var depotId))
                {
                    continue;
                }

                var config = dChild["config"];
                var manifests = dChild["manifests"];

                string? name = dChild["name"].AsString();
                string? osList = config["oslist"].AsString();
                string? language = config["language"].AsString();
                bool steamdeck = config["steamdeck"].AsString() == "1";
                string? size = config["maxsize"].AsString() ?? config["size"].AsString();

                ManifestGid? publicManifestGid = null;
                var manifestsMap = new Dictionary<string, ManifestGid>();

                if (manifests != KeyValue.Invalid)
                {
                    foreach (var mChild in manifests.Children)
                    {
                        string mBranch = mChild.Name ?? "public";
                        string? gidStr = mChild["gid"].AsString() ?? mChild.AsString();

                        if (ManifestGid.TryParse(gidStr, out var gid))
                        {
                            manifestsMap[mBranch] = gid;
                            if (string.Equals(mBranch, "public", StringComparison.OrdinalIgnoreCase))
                            {
                                publicManifestGid = gid;
                            }
                        }
                    }
                }

                depots[depotId] = new SteamDepotInfo(
                    depotId,
                    name,
                    osList,
                    language,
                    steamdeck,
                    size,
                    publicManifestGid,
                    manifestsMap);
            }
        }

        return new SteamAppMetadata(
            appId,
            appName,
            installDir,
            headerUrl,
            buildId,
            timeUpdated,
            depots,
            branches,
            source: "steam_client");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _cts.Cancel();
        _channel.Writer.TryComplete();
        _cts.Dispose();
    }
}
