using FluentAssertions;
using Potato.Domain.ValueObjects;
using Potato.Downloader.Options;
using Xunit;

namespace Potato.Downloader.Tests.Options;

public class DepotDownloaderOptionsTests
{
    [Fact]
    public void BuildCommandLineArgs_ShouldGenerateExactFlagsAndOrder()
    {
        var options = new DepotDownloaderOptions
        {
            AppId = new AppId(746850),
            DepotId = new DepotId(746851),
            ManifestGid = new ManifestGid(5225699216215765938),
            ManifestFilePath = "/tmp/746851_5225699216215765938.manifest",
            DepotKeysFilePath = "/tmp/keys.vdf",
            DownloadDir = "/games/Cloudpunk",
            MaxDownloads = 8,
            Validate = true,
            Branch = "testing",
            UseLanCache = true,
            LoginId = 123456789,
            FileListPath = "/tmp/files.txt",
            DepotDownloaderDllPath = "/deps/DepotDownloader.dll"
        };

        var args = options.BuildCommandLineArgs();

        args.Should().ContainInOrder(
            "/deps/DepotDownloader.dll",
            "-app", "746850",
            "-depot", "746851",
            "-manifest", "5225699216215765938",
            "-manifestfile", "/tmp/746851_5225699216215765938.manifest",
            "-depotkeys", "/tmp/keys.vdf",
            "-max-downloads", "8",
            "-dir", "/games/Cloudpunk",
            "-validate",
            "-branch", "testing",
            "-use-lancache",
            "-loginid", "123456789",
            "-filelist", "/tmp/files.txt"
        );
    }

    [Fact]
    public void BuildCommandLineArgs_ShouldOmitOptionalFlagsWhenDefault()
    {
        var options = new DepotDownloaderOptions
        {
            AppId = new AppId(480),
            DepotId = new DepotId(481),
            ManifestGid = new ManifestGid(1000000000),
            ManifestFilePath = "/tmp/manifest.manifest",
            DepotKeysFilePath = "/tmp/keys.vdf",
            DownloadDir = "/games/Spacewar",
            MaxDownloads = 4,
            Validate = false,
            Branch = "public",
            UseLanCache = false,
            FileListPath = null,
            DepotDownloaderDllPath = "/deps/DepotDownloader.dll"
        };

        var args = options.BuildCommandLineArgs();

        args.Should().NotContain("-validate");
        args.Should().NotContain("-branch");
        args.Should().NotContain("-use-lancache");
        args.Should().NotContain("-filelist");
        args.Should().Contain("-loginid");
    }
}
