using Potato.Queue.Models;

namespace Potato.Queue.Events;

public sealed class QueueJobFailedEventArgs : QueueJobEventArgs
{
    public string ErrorMessage { get; }
    public Exception? Exception { get; }

    public QueueJobFailedEventArgs(QueueJob job, string errorMessage, Exception? exception = null)
        : base(job)
    {
        ErrorMessage = errorMessage ?? "Unknown installation error.";
        Exception = exception;
    }
}
