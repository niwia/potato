using Potato.Domain.ValueObjects;
using Potato.Library.Models;

namespace Potato.Library.Services;

/// <summary>
/// Service for recording, retrieving, and persisting recent pipeline activities.
/// </summary>
public interface IActivityLogService
{
    event EventHandler<ActivityLogEntry>? ActivityAdded;

    IReadOnlyList<ActivityLogEntry> GetRecentActivities(int limit = 20);

    void RecordActivity(ActivityLogEntry entry);

    void RecordSuccess(AppId appId, string gameName, ulong totalBytes, TimeSpan duration, string? details = null);

    void RecordFailure(AppId appId, string gameName, string errorMessage);
}
