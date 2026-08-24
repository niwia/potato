using Potato.Queue.Models;

namespace Potato.Queue.Events;

public sealed class QueueSummaryEventArgs : EventArgs
{
    public QueueSummary Summary { get; }

    public QueueSummaryEventArgs(QueueSummary summary)
    {
        Summary = summary ?? throw new ArgumentNullException(nameof(summary));
    }
}
