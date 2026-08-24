using Potato.Pipeline.Models;
using Potato.Queue.Models;

namespace Potato.Queue.Events;

public sealed class QueueJobProgressEventArgs : QueueJobEventArgs
{
    public InstallProgressReport ProgressReport { get; }

    public QueueJobProgressEventArgs(QueueJob job, InstallProgressReport progressReport)
        : base(job)
    {
        ProgressReport = progressReport ?? throw new ArgumentNullException(nameof(progressReport));
    }
}
