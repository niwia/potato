using FluentAssertions;
using Potato.Downloader.Progress;
using Xunit;

namespace Potato.Downloader.Tests.Progress;

public class DownloadProgressParserTests
{
    [Theory]
    [InlineData("12.50%", 12.50)]
    [InlineData("0.00%", 0.00)]
    [InlineData("100.00%", 100.00)]
    [InlineData("Processing chunk 482... 45.8% done", 45.8)]
    [InlineData("99%", 99.0)]
    public void ProcessLine_ShouldExtractPercentageFromStdout(string inputLine, double expectedPercentage)
    {
        var parser = new DownloadProgressParser();

        var report = parser.ProcessLine(inputLine, currentTimeSeconds: 10.0);

        report.Should().NotBeNull();
        report!.Percentage.Should().BeApproximately(expectedPercentage, 0.01);
    }

    [Theory]
    [InlineData("Validating chunk 1 of 500")]
    [InlineData("Checking local installation...")]
    public void ProcessLine_ShouldDetectValidationState(string inputLine)
    {
        var parser = new DownloadProgressParser();

        var report = parser.ProcessLine(inputLine);

        report.Should().NotBeNull();
        report!.IsValidating.Should().BeTrue();
    }

    [Fact]
    public void ProcessLine_ShouldIgnoreUnrelatedLines()
    {
        var parser = new DownloadProgressParser();

        var report = parser.ProcessLine("Connecting to Steam3 CDN...");

        report.Should().BeNull();
    }

    [Fact]
    public void ProcessLine_ShouldCalculateExponentialMovingAverageSpeed()
    {
        // 100 MB total
        ulong totalSize = 100 * 1024 * 1024;
        var parser = new DownloadProgressParser(totalDownloadSize: totalSize, currentDepotSize: totalSize);

        // First sample at t = 10.0: 10% (10 MB)
        var report1 = parser.ProcessLine("10.00%", currentTimeSeconds: 10.0);
        report1.Should().NotBeNull();

        // Second sample at t = 12.0 (2 seconds elapsed): 30% (30 MB -> diff = 20 MB -> instSpeed = 10 MB/s)
        var report2 = parser.ProcessLine("30.00%", currentTimeSeconds: 12.0);
        report2.Should().NotBeNull();
        report2!.SpeedBytesPerSecond.Should().BeApproximately(10 * 1024 * 1024, 100);
        report2.FormattedSpeed.Should().Be("10.00 MB/s");

        // Third sample at t = 14.0 (2 seconds elapsed): 40% (40 MB -> diff = 10 MB -> instSpeed = 5 MB/s)
        // EMA: 0.35 * 5 MB/s + 0.65 * 10 MB/s = 1.75 + 6.5 = 8.25 MB/s
        var report3 = parser.ProcessLine("40.00%", currentTimeSeconds: 14.0);
        report3.Should().NotBeNull();
        report3!.SpeedBytesPerSecond.Should().BeApproximately(8.25 * 1024 * 1024, 100);
        report3.FormattedSpeed.Should().Be("8.25 MB/s");
    }

    [Fact]
    public void ProcessLine_ShouldCalculateAccurateEta()
    {
        // 100 MB total
        ulong totalSize = 100 * 1024 * 1024;
        var parser = new DownloadProgressParser(totalDownloadSize: totalSize, currentDepotSize: totalSize);

        // First sample: 0% at t = 0
        parser.ProcessLine("0.00%", currentTimeSeconds: 0.0);

        // Second sample: 50% at t = 10 (50 MB in 10s = 5 MB/s speed -> remaining 50 MB -> ETA 10s)
        var report = parser.ProcessLine("50.00%", currentTimeSeconds: 10.0);

        report.Should().NotBeNull();
        report!.EstimatedTimeRemaining.Should().NotBeNull();
        ((int)report.EstimatedTimeRemaining!.Value.TotalSeconds).Should().Be(10);
        report.FormattedEta.Should().Be("10s remaining");
    }

    [Fact]
    public void FormatSpeed_ShouldFormatCorrectUnits()
    {
        DownloadProgressParser.FormatSpeed(500).Should().Be("500.00 B/s");
        DownloadProgressParser.FormatSpeed(1536).Should().Be("1.50 KB/s");
        DownloadProgressParser.FormatSpeed(10485760).Should().Be("10.00 MB/s");
    }

    [Fact]
    public void FormatEta_ShouldFormatVariousDurations()
    {
        DownloadProgressParser.FormatEta(TimeSpan.FromSeconds(45)).Should().Be("45s remaining");
        DownloadProgressParser.FormatEta(TimeSpan.FromMinutes(2.5)).Should().Be("2m 30s remaining");
        DownloadProgressParser.FormatEta(TimeSpan.FromHours(1.5)).Should().Be("1h 30m remaining");
    }
}
