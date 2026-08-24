using System.Text.Json;
using Potato.Domain.ValueObjects;
using Potato.Library.Models;

namespace Potato.Library.Services;

public sealed class ActivityLogService : IActivityLogService
{
    private readonly string _logFilePath;
    private readonly List<ActivityLogEntry> _activities = new();
    private readonly object _lock = new();

    public event EventHandler<ActivityLogEntry>? ActivityAdded;

    public ActivityLogService(string? customLogPath = null)
    {
        if (!string.IsNullOrWhiteSpace(customLogPath))
        {
            _logFilePath = customLogPath;
        }
        else
        {
            string userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string configDir = Path.Combine(userHome, ".config", "potato");
            if (!Directory.Exists(configDir))
            {
                Directory.CreateDirectory(configDir);
            }
            _logFilePath = Path.Combine(configDir, "recent_activity.json");
        }

        LoadFromDisk();
    }

    public IReadOnlyList<ActivityLogEntry> GetRecentActivities(int limit = 20)
    {
        lock (_lock)
        {
            return _activities.Take(limit).ToList();
        }
    }

    public void RecordActivity(ActivityLogEntry entry)
    {
        if (entry == null) return;

        lock (_lock)
        {
            _activities.Insert(0, entry);
            if (_activities.Count > 100)
            {
                _activities.RemoveRange(100, _activities.Count - 100);
            }

            SaveToDisk();
        }

        ActivityAdded?.Invoke(this, entry);
    }

    public void RecordSuccess(AppId appId, string gameName, ulong totalBytes, TimeSpan duration, string? details = null)
    {
        var entry = new ActivityLogEntry(
            Guid.NewGuid(),
            appId,
            gameName,
            ActivityStatus.Success,
            DateTime.UtcNow,
            totalBytes,
            duration,
            details ?? "Depots: Verified • SLS: Synchronized");

        RecordActivity(entry);
    }

    public void RecordFailure(AppId appId, string gameName, string errorMessage)
    {
        var entry = new ActivityLogEntry(
            Guid.NewGuid(),
            appId,
            gameName,
            ActivityStatus.Failed,
            DateTime.UtcNow,
            ErrorMessage: errorMessage);

        RecordActivity(entry);
    }

    private void LoadFromDisk()
    {
        try
        {
            if (File.Exists(_logFilePath))
            {
                string json = File.ReadAllText(_logFilePath);
                var loaded = JsonSerializer.Deserialize<List<ActivityLogEntry>>(json);
                if (loaded != null)
                {
                    _activities.AddRange(loaded);
                }
            }
        }
        catch { }
    }

    private void SaveToDisk()
    {
        try
        {
            string json = JsonSerializer.Serialize(_activities, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_logFilePath, json);
        }
        catch { }
    }
}
