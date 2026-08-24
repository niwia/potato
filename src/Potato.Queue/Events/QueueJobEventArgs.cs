using Potato.Queue.Models;

namespace Potato.Queue.Events;

public class QueueJobEventArgs : EventArgs
{
    public QueueJob Job { get; }

    public QueueJobEventArgs(QueueJob job)
    {
        Job = job ?? throw new ArgumentNullException(nameof(job));
    }
}
