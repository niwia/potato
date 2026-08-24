using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Potato.Downloader.Progress;

/// <summary>
/// Parses raw stdout stream lines from DepotDownloaderMod (both structured JSON and legacy regex),
/// extracts progress percentages, applies Exponential Moving Average (EMA) smoothing for speed calculations, and computes ETA.
/// </summary>
public sealed class DownloadProgressParser
{
    private static readonly Regex PercentageRegex = new(@"(\d{1,3}(?:\.\d{1,2})?)%", RegexOptions.Compiled);

    public ulong TotalDownloadSize { get; set; }
    public ulong CompletedSoFar { get; set; }
    public ulong CurrentDepotSize { get; set; }
    public bool IsValidating { get; private set; }

    private double _smoothSpeedBps;
    private double? _lastSpeedCalcTime;
    private double _lastDownloadedBytes;

    public event Action<DownloadProgressReport>? ProgressChanged;

    public DownloadProgressParser(ulong totalDownloadSize = 0, ulong currentDepotSize = 0)
    {
        TotalDownloadSize = totalDownloadSize;
        CurrentDepotSize = currentDepotSize > 0 ? currentDepotSize : totalDownloadSize;
    }

    public void Reset()
    {
        _smoothSpeedBps = 0.0;
        _lastSpeedCalcTime = null;
        _lastDownloadedBytes = 0.0;
        IsValidating = false;
    }

    /// <summary>
    /// Processes a single stdout line (split on \r or \n).
    /// </summary>
    /// <param name="line">Line text</param>
    /// <param name="currentTimeSeconds">Optional explicit timestamp for deterministic unit testing</param>
    /// <returns>Updated progress report if a progress or validation event occurred, otherwise null.</returns>
    public DownloadProgressReport? ProcessLine(string? line, double? currentTimeSeconds = null)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        string trimmed = line.Trim();
        double now = currentTimeSeconds ?? (DateTime.UtcNow.Ticks / (double)TimeSpan.TicksPerSecond);

        // 1. Try fast-path JSON Progress parsing
        if (trimmed.StartsWith("{") && trimmed.Contains("\"type\":\"progress\""))
        {
            try
            {
                using var doc = JsonDocument.Parse(trimmed);
                var root = doc.RootElement;

                uint depotId = root.TryGetProperty("depotId", out var dIdProp) ? dIdProp.GetUInt32() : 0;
                ulong bytesDownloaded = root.TryGetProperty("bytesDownloaded", out var bdProp) ? bdProp.GetUInt64() : 0;
                ulong totalBytes = root.TryGetProperty("totalBytes", out var tbProp) ? tbProp.GetUInt64() : (CurrentDepotSize > 0 ? CurrentDepotSize : TotalDownloadSize);
                double percentage = root.TryGetProperty("percentage", out var pctProp) ? pctProp.GetDouble() : 0.0;
                bool isVal = root.TryGetProperty("isValidating", out var valProp) && valProp.GetBoolean();
                string? currentFile = root.TryGetProperty("currentFile", out var fileProp) && fileProp.ValueKind == JsonValueKind.String
                    ? fileProp.GetString()
                    : null;

                IsValidating = isVal;

                double totalProgressBytes;
                double overallPercentage;

                if (TotalDownloadSize > 0 && CurrentDepotSize > 0)
                {
                    totalProgressBytes = CompletedSoFar + bytesDownloaded;
                    overallPercentage = (totalProgressBytes / TotalDownloadSize) * 100.0;
                }
                else
                {
                    totalProgressBytes = bytesDownloaded;
                    overallPercentage = percentage;
                }

                overallPercentage = Math.Clamp(overallPercentage, 0.0, 100.0);

                // Speed & EMA smoothing
                UpdateSpeed(totalProgressBytes, now);

                double speed = _smoothSpeedBps;
                string speedStr = FormatSpeed(speed);

                // ETA
                TimeSpan? eta = null;
                string etaStr = "Calculating...";
                if (TotalDownloadSize > 0 && speed > 1024)
                {
                    double remainingBytes = Math.Max(0.0, (double)TotalDownloadSize - totalProgressBytes);
                    double etaSeconds = remainingBytes / speed;
                    eta = TimeSpan.FromSeconds(etaSeconds);
                    etaStr = FormatEta(eta.Value);
                }

                var report = new DownloadProgressReport
                {
                    Percentage = overallPercentage,
                    SpeedBytesPerSecond = speed,
                    FormattedSpeed = speedStr,
                    EstimatedTimeRemaining = eta,
                    FormattedEta = etaStr,
                    DownloadedBytes = TotalDownloadSize > 0 ? (ulong)Math.Min((double)TotalDownloadSize, totalProgressBytes) : (ulong)totalProgressBytes,
                    TotalBytes = TotalDownloadSize > 0 ? TotalDownloadSize : totalBytes,
                    IsValidating = isVal,
                    CurrentFile = currentFile,
                    DepotId = depotId > 0 ? depotId : null,
                    RawLine = trimmed
                };

                ProgressChanged?.Invoke(report);
                return report;
            }
            catch
            {
                // Fall back to legacy parser on malformed JSON
            }
        }

        // 2. Check for legacy validation phase
        bool isValidation = trimmed.StartsWith("Validating ", StringComparison.OrdinalIgnoreCase) ||
                            trimmed.StartsWith("Checking ", StringComparison.OrdinalIgnoreCase);

        if (isValidation)
        {
            IsValidating = true;
            var report = new DownloadProgressReport
            {
                Percentage = 0,
                SpeedBytesPerSecond = 0,
                FormattedSpeed = "0.00 B/s",
                EstimatedTimeRemaining = null,
                FormattedEta = "Calculating...",
                DownloadedBytes = CompletedSoFar,
                TotalBytes = TotalDownloadSize,
                IsValidating = true,
                RawLine = trimmed
            };

            ProgressChanged?.Invoke(report);
            return report;
        }

        // 3. Check for legacy regex percentage update
        var match = PercentageRegex.Match(trimmed);
        if (match.Success && double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double depotPercentage))
        {
            IsValidating = false;

            double totalProgressBytes;
            double overallPercentage;

            if (TotalDownloadSize > 0 && CurrentDepotSize > 0)
            {
                double currentDepotProgress = (depotPercentage / 100.0) * CurrentDepotSize;
                totalProgressBytes = CompletedSoFar + currentDepotProgress;
                overallPercentage = (totalProgressBytes / TotalDownloadSize) * 100.0;
            }
            else
            {
                totalProgressBytes = depotPercentage;
                overallPercentage = depotPercentage;
            }

            overallPercentage = Math.Clamp(overallPercentage, 0.0, 100.0);

            // Calculate Speed & EMA smoothing
            UpdateSpeed(totalProgressBytes, now);

            double speed = _smoothSpeedBps;
            string speedStr = FormatSpeed(speed);

            // Calculate ETA
            TimeSpan? eta = null;
            string etaStr = "Calculating...";

            if (TotalDownloadSize > 0 && speed > 1024)
            {
                double remainingBytes = Math.Max(0.0, (double)TotalDownloadSize - totalProgressBytes);
                double etaSeconds = remainingBytes / speed;
                eta = TimeSpan.FromSeconds(etaSeconds);
                etaStr = FormatEta(eta.Value);
            }

            var report = new DownloadProgressReport
            {
                Percentage = overallPercentage,
                SpeedBytesPerSecond = speed,
                FormattedSpeed = speedStr,
                EstimatedTimeRemaining = eta,
                FormattedEta = etaStr,
                DownloadedBytes = TotalDownloadSize > 0 ? (ulong)Math.Min((double)TotalDownloadSize, totalProgressBytes) : (ulong)totalProgressBytes,
                TotalBytes = TotalDownloadSize,
                IsValidating = false,
                RawLine = trimmed
            };

            ProgressChanged?.Invoke(report);
            return report;
        }

        return null;
    }

    private void UpdateSpeed(double totalProgressBytes, double now)
    {
        if (_lastSpeedCalcTime == null)
        {
            _lastSpeedCalcTime = now;
            _lastDownloadedBytes = totalProgressBytes;
        }
        else if (now - _lastSpeedCalcTime.Value >= 0.5)
        {
            double elapsed = now - _lastSpeedCalcTime.Value;
            double bytesDiff = totalProgressBytes - _lastDownloadedBytes;
            _lastDownloadedBytes = totalProgressBytes;
            _lastSpeedCalcTime = now;

            if (bytesDiff < 0) bytesDiff = 0;

            double instSpeed = bytesDiff / elapsed;

            // Exponential Moving Average with alpha = 0.35
            if (_smoothSpeedBps == 0.0)
            {
                _smoothSpeedBps = instSpeed;
            }
            else
            {
                _smoothSpeedBps = (0.35 * instSpeed) + (0.65 * _smoothSpeedBps);
            }
        }
    }

    public static string FormatSpeed(double speedBps)
    {
        if (speedBps < 1024)
        {
            return $"{speedBps:F2} B/s";
        }
        if (speedBps < 1024 * 1024)
        {
            return $"{(speedBps / 1024.0):F2} KB/s";
        }
        return $"{(speedBps / (1024.0 * 1024.0)):F2} MB/s";
    }

    public static string FormatEta(TimeSpan eta)
    {
        int totalSeconds = (int)eta.TotalSeconds;
        if (totalSeconds < 60)
        {
            return $"{totalSeconds}s remaining";
        }
        if (totalSeconds < 3600)
        {
            return $"{totalSeconds / 60}m {totalSeconds % 60}s remaining";
        }
        return $"{totalSeconds / 3600}h {(totalSeconds % 3600) / 60}m remaining";
    }
}
