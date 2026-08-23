using System.IO;
using System.Threading.Tasks;
using Xunit;
using Potato.Core.Models;
using Potato.Core.Steam;
using Potato.Core.Slssteam;
using Potato.Core.Storage;
using Potato.Downloader;

namespace Potato.Tests;

public class CoreTests
{
    [Fact]
    public void AcfManager_WriteAndRead_Roundtrip()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "potato_test_" + Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);

        try
        {
            uint appId = 730;
            string gameName = "Counter-Strike 2";
            string installDir = "Counter-Strike Global Offensive";
            var depots = new[]
            {
                new DepotInfo { DepotId = 731, Name = "CS2 Content", ManifestId = 1234567890123456789, SizeBytes = 1048576, OsList = "windows" },
                new DepotInfo { DepotId = 732, Name = "CS2 Binaries", ManifestId = 9876543210987654321, SizeBytes = 2097152, OsList = "windows" }
            };

            // Write ACF
            bool writeSuccess = AcfManager.WriteAcf(tempDir, appId, gameName, installDir, depots, buildId: 55555, totalSize: 3145728);
            Assert.True(writeSuccess);

            // Read ACF back
            var acfPath = AcfManager.GetAcfPath(tempDir, appId);
            Assert.True(File.Exists(acfPath));

            var app = AcfManager.ReadAcf(acfPath);
            Assert.NotNull(app);
            Assert.Equal(appId, app.AppId);
            Assert.Equal(gameName, app.Name);
            Assert.Equal(installDir, app.InstallDir);
            Assert.Equal((ulong)55555, app.BuildId);
            Assert.Equal(2, app.MountedDepots.Count);
            Assert.Equal((ulong)1234567890123456789, app.MountedDepots[731]);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void SlsConfigManager_AddAndRemoveApp()
    {
        var tempFile = Path.GetTempFileName();

        try
        {
            File.WriteAllText(tempFile, "AdditionalApps:\n  - 1091500 # Cyberpunk 2077\n");

            // Add App
            SlsConfigManager.AddAdditionalApp(tempFile, 730, "Counter-Strike 2");

            var apps = SlsConfigManager.GetAdditionalApps(tempFile);
            Assert.Contains((uint)1091500, apps);
            Assert.Contains((uint)730, apps);

            // Remove App
            SlsConfigManager.RemoveAdditionalApp(tempFile, 1091500);
            var appsAfter = SlsConfigManager.GetAdditionalApps(tempFile);
            Assert.DoesNotContain((uint)1091500, appsAfter);
            Assert.Contains((uint)730, appsAfter);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
            if (File.Exists(tempFile + ".bak")) File.Delete(tempFile + ".bak");
        }
    }

    [Fact]
    public async Task DatabaseManager_HeaderAndDepotKey_Storage()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "potato_db_" + Path.GetRandomFileName());

        try
        {
            var db = new DatabaseManager(tempDir);

            // Test header cache
            await db.CacheHeaderAsync(730, "Counter-Strike 2", "https://cdn.cloudflare.steamstatic.com/header.jpg");
            var (name, url) = await db.GetHeaderAsync(730);
            Assert.Equal("Counter-Strike 2", name);
            Assert.Equal("https://cdn.cloudflare.steamstatic.com/header.jpg", url);

            // Test depot key cache
            await db.StoreDepotKeyAsync(731, "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF");
            var key = await db.GetDepotKeyAsync(731);
            Assert.Equal("0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF", key);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void SpeedMonitor_FormatSpeedAndBytes()
    {
        Assert.Equal("10.0 MB/s", SpeedMonitor.FormatSpeed(10 * 1024 * 1024));
        Assert.Equal("500.0 KB/s", SpeedMonitor.FormatSpeed(500 * 1024));
        Assert.Equal("1.50 GB", SpeedMonitor.FormatBytes((long)(1.5 * 1024 * 1024 * 1024)));
    }
}
