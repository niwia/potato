namespace Potato.Downloader;

public class SpeedMonitor
{
    private readonly object _lock = new();
    private long _lastBytes;
    private DateTime _lastTime = DateTime.UtcNow;
    private double _smoothedSpeed;
    private const double Alpha = 0.3; // EMA smoothing factor

    public void Reset()
    {
        lock (_lock)
        {
            _lastBytes = 0;
            _lastTime = DateTime.UtcNow;
            _smoothedSpeed = 0;
        }
    }

    public double UpdateSpeed(long currentBytes)
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;
            var elapsed = (now - _lastTime).TotalSeconds;

            if (elapsed >= 0.5)
            {
                var deltaBytes = Math.Max(0, currentBytes - _lastBytes);
                var instantSpeed = deltaBytes / elapsed;

                _smoothedSpeed = (_smoothedSpeed == 0)
                    ? instantSpeed
                    : (Alpha * instantSpeed) + ((1 - Alpha) * _smoothedSpeed);

                _lastBytes = currentBytes;
                _lastTime = now;
            }

            return _smoothedSpeed;
        }
    }

    public static string FormatSpeed(double bytesPerSec)
    {
        if (bytesPerSec >= 1024 * 1024 * 1024)
            return $"{bytesPerSec / (1024 * 1024 * 1024):0.00} GB/s";
        if (bytesPerSec >= 1024 * 1024)
            return $"{bytesPerSec / (1024 * 1024):0.0} MB/s";
        if (bytesPerSec >= 1024)
            return $"{bytesPerSec / 1024:0.0} KB/s";
        return $"{bytesPerSec:0} B/s";
    }

    public static string FormatBytes(long bytes)
    {
        if (bytes >= 1024L * 1024 * 1024)
            return $"{bytes / (1024.0 * 1024 * 1024):0.00} GB";
        if (bytes >= 1024L * 1024)
            return $"{bytes / (1024.0 * 1024):0.0} MB";
        if (bytes >= 1024L)
            return $"{bytes / 1024.0:0.0} KB";
        return $"{bytes} B";
    }

    public static TimeSpan CalculateEta(long downloadedBytes, long totalBytes, double speedBytesPerSec)
    {
        if (speedBytesPerSec <= 0 || totalBytes <= downloadedBytes)
        {
            return TimeSpan.Zero;
        }

        var remainingBytes = totalBytes - downloadedBytes;
        var seconds = remainingBytes / speedBytesPerSec;
        return TimeSpan.FromSeconds(Math.Min(seconds, 86400 * 7)); // Cap at 7 days
    }
}
