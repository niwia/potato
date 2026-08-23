# 🥔 Potato

**Potato** is a high-performance, modular Steam game deployment engine and orchestrator for Linux and Steam Deck, built in **C# (.NET 9)**.

---

## 🏗️ Architecture

Potato is engineered strictly bottom-up with decoupled, independently testable domain libraries:

```text
potato/
├── deps/
│   └── DepotDownloader/          # Bundled DepotDownloaderMod runtime dependencies
├── src/
│   ├── Potato.Domain/            # Pure domain models (Game, Depot, Manifest), VDF parser/serializer, AcfManager
│   ├── Potato.Downloader/        # Out-of-process DepotDownloaderMod runner, stream parser, pause/resume suspender
│   ├── Potato.ManifestApi/       # 4-tier Hubcap manifest resolution engine (local cache, single, bundle, zip) & quota tracking
│   ├── Potato.SteamMetadata/     # 4-layer Steam metadata resolver (SQLite Zstd cache, SteamCMD REST, SteamKit2 PICS, Storefront)
│   ├── Potato.Pipeline/          # Full 5-stage installation pipeline orchestrator & SQLite depot keys store
│   └── Potato.Downloader.ConsoleHarness/ # Comprehensive CLI testing harness for downloading, manifests, metadata, & full install
└── tests/
    ├── Potato.Domain.Tests/
    ├── Potato.Downloader.Tests/
    ├── Potato.ManifestApi.Tests/
    ├── Potato.SteamMetadata.Tests/
    └── Potato.Pipeline.Tests/
```

---

## 🚀 CLI Console Harness

Run the console harness to execute individual steps or the complete end-to-end installation pipeline:

### 1. Full Game Installation (Pipeline Orchestration)
```bash
dotnet run --project src/Potato.Downloader.ConsoleHarness -- --install --app <appid> [--dir <library_path>] [--branch <name>] [--depot <depotid>]
```

### 2. Resolve Steam Metadata (4-Layer Engine)
```bash
dotnet run --project src/Potato.Downloader.ConsoleHarness -- --resolve-metadata --app <appid> [--token <token>] [--force-refresh]
```

### 3. Resolve Manifests (4-Tier Hubcap Engine)
```bash
dotnet run --project src/Potato.Downloader.ConsoleHarness -- --resolve-manifest --app <appid> [--depot <depot>] [--manifest <gid>] [--branch <name>]
```

### 4. Direct Depot Download
```bash
dotnet run --project src/Potato.Downloader.ConsoleHarness -- --app <appid> --depot <depotid> --manifest <gid> --manifestfile <path> --dir <download_dir>
```

---

## 🧪 Testing

Run the full automated test suite (56 tests across 5 projects):

```bash
dotnet test Potato.sln
```

---

## 📜 License

MIT License.
