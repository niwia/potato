namespace Potato.Core.Models;

public record RecentActivityItem
{
    public string GameName { get; init; } = string.Empty;
    public uint AppId { get; init; }
    public string Timestamp { get; init; } = string.Empty;
    public string MetricsSummary { get; init; } = string.Empty;
    public string Details { get; init; } = string.Empty;
    public bool IsSuccess { get; init; } = true;
}
