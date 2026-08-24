using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Potato.Queue.Manager;
using Potato.Queue.Models;

namespace Potato.UI.ViewModels;

public sealed partial class QueueViewModel : ViewModelBase
{
    private readonly IDownloadQueueManager _queueManager;

    [ObservableProperty]
    private int _runningCount;

    [ObservableProperty]
    private int _queuedCount;

    [ObservableProperty]
    private int _completedCount;

    [ObservableProperty]
    private double _aggregateSpeedBps;

    [ObservableProperty]
    private string _aggregateSpeedFormatted = "0.00 B/s";

    public ObservableCollection<QueueJobItemViewModel> Jobs { get; } = new();

    public QueueViewModel(IDownloadQueueManager queueManager)
    {
        _queueManager = queueManager;

        _queueManager.JobEnqueued += (s, e) => Dispatcher.UIThread.Post(() =>
        {
            if (!Jobs.Any(j => j.Id == e.Job.Id))
            {
                Jobs.Add(new QueueJobItemViewModel(e.Job, _queueManager));
            }
            RefreshSummary();
        });

        _queueManager.JobStarted += (s, e) => Dispatcher.UIThread.Post(() =>
        {
            var vm = Jobs.FirstOrDefault(j => j.Id == e.Job.Id);
            vm?.UpdateStatus(QueueJobStatus.Running);
            RefreshSummary();
        });

        _queueManager.JobProgressUpdated += (s, e) => Dispatcher.UIThread.Post(() =>
        {
            var vm = Jobs.FirstOrDefault(j => j.Id == e.Job.Id);
            if (vm != null)
            {
                var dp = e.ProgressReport.DownloadProgress;
                if (dp != null)
                {
                    vm.UpdateProgress(dp.Percentage, dp.FormattedSpeed, dp.FormattedEta, dp.CurrentFile);
                }
            }
        });

        _queueManager.JobCompleted += (s, e) => Dispatcher.UIThread.Post(() =>
        {
            var vm = Jobs.FirstOrDefault(j => j.Id == e.Job.Id);
            vm?.UpdateStatus(QueueJobStatus.Completed);
            RefreshSummary();
        });

        _queueManager.JobFailed += (s, e) => Dispatcher.UIThread.Post(() =>
        {
            var vm = Jobs.FirstOrDefault(j => j.Id == e.Job.Id);
            vm?.UpdateStatus(QueueJobStatus.Failed, e.ErrorMessage);
            RefreshSummary();
        });

        _queueManager.JobStateChanged += (s, e) => Dispatcher.UIThread.Post(() =>
        {
            var vm = Jobs.FirstOrDefault(j => j.Id == e.Job.Id);
            vm?.UpdateStatus(e.Job.Status, e.Job.ErrorMessage);
            RefreshSummary();
        });

        _queueManager.QueueSummaryUpdated += (s, e) => Dispatcher.UIThread.Post(() =>
        {
            RunningCount = e.Summary.RunningCount;
            QueuedCount = e.Summary.QueuedCount;
            CompletedCount = e.Summary.CompletedCount;
            AggregateSpeedBps = e.Summary.AggregateDownloadSpeedBytesPerSecond;
            AggregateSpeedFormatted = e.Summary.FormattedSpeed;
        });
    }

    [RelayCommand]
    public void PauseAll() => _queueManager.PauseAll();

    [RelayCommand]
    public void ResumeAll() => _queueManager.ResumeAll();

    [RelayCommand]
    public void ClearCompleted()
    {
        _queueManager.ClearCompleted();
        var terminal = Jobs.Where(j => j.Model.IsTerminal).ToList();
        foreach (var t in terminal)
        {
            Jobs.Remove(t);
        }
        RefreshSummary();
    }

    private void RefreshSummary()
    {
        var s = _queueManager.GetSummary();
        RunningCount = s.RunningCount;
        QueuedCount = s.QueuedCount;
        CompletedCount = s.CompletedCount;
        AggregateSpeedBps = s.AggregateDownloadSpeedBytesPerSecond;
        AggregateSpeedFormatted = s.FormattedSpeed;
    }
}
