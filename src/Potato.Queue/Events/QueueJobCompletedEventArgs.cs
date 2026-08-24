using Potato.Pipeline.Models;
using Potato.Queue.Models;

namespace Potato.Queue.Events;

public sealed class QueueJobCompletedEventArgs : QueueJobEventArgs
{
    public InstallResult Result { get; }

    public QueueJobCompletedEventArgs(QueueJob job, InstallResult result)
        : base(job)
    {
        Result = result ?? throw new ArgumentNullException(nameof(result));
    }
}
