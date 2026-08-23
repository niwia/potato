namespace Potato.Domain.Models;

/// <summary>
/// Represents a download job operation targeting specific depots of a game.
/// </summary>
public sealed class DownloadJob
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Game Game { get; init; }
    public IReadOnlyList<Depot> TargetDepots { get; init; }
    public string TargetPath { get; init; }
    public DownloadStatus Status { get; set; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }

    public DownloadJob(
        Game game,
        IReadOnlyList<Depot> targetDepots,
        string targetPath,
        DownloadStatus status = DownloadStatus.Queued)
    {
        Game = game ?? throw new ArgumentNullException(nameof(game));
        TargetDepots = targetDepots ?? Array.Empty<Depot>();
        TargetPath = targetPath ?? string.Empty;
        Status = status;
    }
}
