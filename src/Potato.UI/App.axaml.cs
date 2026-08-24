using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Potato.Configuration.Services;
using Potato.Downloader.Process;
using Potato.Library.Services;
using Potato.ManifestApi.Cache;
using Potato.ManifestApi.Client;
using Potato.ManifestApi.Models;
using Potato.ManifestApi.Quota;
using Potato.Pipeline.Keys;
using Potato.Pipeline.Orchestrator;
using Potato.Queue.Manager;
using Potato.SlsSteam.Config;
using Potato.SlsSteam.Ipc;
using Potato.SlsSteam.Paths;
using Potato.SteamMetadata.Clients;
using Potato.SteamMetadata.Resolver;
using Potato.SteamMetadata.Storage;
using Potato.UI.ViewModels;
using Potato.UI.Views;

namespace Potato.UI;

public partial class App : Application
{
    public IServiceProvider? Services { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        Console.WriteLine("[UI] Building Dependency Injection Service Provider...");
        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        // Initialize and load settings synchronously before window instantiation
        var settingsService = Services.GetRequiredService<ISettingsService>();
        settingsService.LoadAsync().GetAwaiter().GetResult();
        Console.WriteLine($"[CONFIG] Settings loaded from: {settingsService.SettingsFilePath}");

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            Console.WriteLine("[UI] Instantiating Main Window and ViewModels...");
            var mainVm = Services.GetRequiredService<MainViewModel>();
            desktop.MainWindow = new MainWindow
            {
                DataContext = mainVm,
            };
            Console.WriteLine("[UI] Main Window initialized and ready.");
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // 1. Configuration
        services.AddSingleton<ISettingsService, SettingsService>();

        // 2. HTTP & Networking
        services.AddSingleton(new HttpClient { Timeout = TimeSpan.FromSeconds(60) });

        // 3. Steam Metadata
        services.AddSingleton<ISteamMetadataStore, SqliteSteamMetadataStore>();
        services.AddSingleton<ISteamCmdRestClient, SteamCmdRestClient>();
        services.AddSingleton<ISteamStoreWebClient, SteamStoreWebClient>();
        services.AddSingleton<ISteamPicsClient, SteamPicsClient>();
        services.AddSingleton<ISteamMetadataResolver, SteamMetadataResolver>();

        // 4. Manifests & API
        services.AddSingleton<IManifestCacheStore, FileManifestCacheStore>();
        services.AddSingleton<QuotaTracker>();
        services.AddSingleton<IHubcapApiClient>(sp =>
        {
            var http = sp.GetRequiredService<HttpClient>();
            var cache = sp.GetRequiredService<IManifestCacheStore>();
            var quota = sp.GetRequiredService<QuotaTracker>();
            var settings = sp.GetRequiredService<ISettingsService>();

            return new HubcapApiClient(http, cache, quota, () =>
            {
                var cur = settings.Current;
                return new HubcapApiOptions
                {
                    ApiKey = cur.Api.HubcapApiKey,
                    BaseUrl = !string.IsNullOrEmpty(cur.Api.CustomWirecutterUrl)
                        ? cur.Api.CustomWirecutterUrl
                        : "https://hubcapmanifest.com/api/v1"
                };
            });
        });

        // 5. Depot Keys & SLSsteam
        services.AddSingleton<IDepotKeyStore, SqliteDepotKeyStore>();
        services.AddSingleton<ISlsSteamPathResolver, SlsSteamPathResolver>();
        services.AddSingleton<ISlsConfigManager, SlsConfigManager>();
        services.AddSingleton<ISlsSteamIpcClient, SlsSteamIpcClient>();

        // 6. Pipeline Orchestrator Factory
        services.AddSingleton<Func<IDepotDownloaderProcess, IInstallGameOrchestrator>>(sp => proc =>
            new InstallGameOrchestrator(
                sp.GetRequiredService<ISteamMetadataResolver>(),
                sp.GetRequiredService<IHubcapApiClient>(),
                sp.GetRequiredService<IDepotKeyStore>(),
                () => proc,
                sp.GetRequiredService<ISlsConfigManager>(),
                sp.GetRequiredService<ISlsSteamIpcClient>(),
                sp.GetRequiredService<ISlsSteamPathResolver>()));

        // 7. Library Services
        services.AddSingleton<ILibraryScanner, LibraryScanner>();
        services.AddSingleton<IGameUpdateChecker, GameUpdateChecker>();
        services.AddSingleton<IGameUninstallService, GameUninstallService>();

        // 8. Queue Manager
        services.AddSingleton<IDownloadQueueManager>(sp =>
            new DownloadQueueManager(sp.GetRequiredService<Func<IDepotDownloaderProcess, IInstallGameOrchestrator>>())
            {
                MaxConcurrentDownloads = sp.GetRequiredService<ISettingsService>().Current.Download.MaxConcurrentQueueJobs
            });

        // 9. ViewModels
        services.AddTransient<LibraryViewModel>();
        services.AddTransient<SearchViewModel>();
        services.AddTransient<QueueViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddSingleton<MainViewModel>();
    }
}