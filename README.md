# 🥔 Potato

**Potato** is a high-performance, native Steam deployment suite and orchestrator for Linux and Steam Deck, built in **C# (.NET 9)** with a hardware-accelerated **Avalonia UI** desktop frontend.

---

## 🚀 Features

- **Store Search & Instant Deploy**: Search games by title or direct numerical Steam App ID.
- **Depot Selection & Filtering**: Multi-column checklist with language, OS, and size filters.
- **Steam Deck & Linux Optimized**: Automatic Proton platform overrides (`UserConfig` / `MountedConfig`), atomic ACF generation, and immutable filesystem compatibility.
- **SLSsteam / Headcrab Integration**: Automatically detects and manages `config.yaml` to hook downloaded games seamlessly into Steam.
- **Installed Games Library**: Built-in library scanner with instant search, one-click folder opening, and SLSsteam hook toggles.
- **Direct Manifest & Rollback**: Direct depot and historical manifest deployment.
- **Ultra Lightweight**: Packaged as a standalone **~43 MB AppImage** (down from ~300 MB) with zero .NET runtime installation required.

---

## 📦 Download & Run

Download the latest `Potato-x86_64.AppImage` from [Releases](https://github.com/niwia/potato/releases), make it executable, and run:

```bash
chmod +x Potato-x86_64.AppImage
./Potato-x86_64.AppImage
```

---

## 🛠️ Building from Source

### Prerequisites
- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)

### 1. Clone the Repository
```bash
git clone git@github.com:niwia/potato.git
cd potato
```

### 2. Run in Development Mode
```bash
dotnet run --project src/Potato.UI/Potato.UI.csproj
```

### 3. Run Automated Tests
```bash
dotnet test tests/Potato.Tests/Potato.Tests.csproj
```

### 4. Build Standalone Linux Executable
```bash
./publish_linux.sh
```
Output: `publish/Potato.UI`

### 5. Build Standalone AppImage
```bash
./build_appimage.sh
```
Output: `dist/Potato-x86_64.AppImage`

---

## 🏗️ Architecture

```
potato/
├── src/
│   ├── Potato.Core/          # UI-agnostic Steam interop, ACF generator, SLSsteam manager, SQLite
│   ├── Potato.Downloader/    # DepotDownloader process manager, speed monitor, async queue
│   └── Potato.UI/            # Avalonia UI MVVM desktop application
└── tests/
    └── Potato.Tests/         # xUnit unit test suite
```

---

## 📜 License

MIT License.
