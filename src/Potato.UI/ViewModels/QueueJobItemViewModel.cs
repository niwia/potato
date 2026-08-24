using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Potato.Domain.ValueObjects;
using Potato.Queue.Manager;
using Potato.Queue.Models;

namespace Potato.UI.ViewModels;

public sealed partial class QueueJobItemViewModel : ObservableObject
{
    private readonly IDownloadQueueManager _queueManager;
    public QueueJob Model { get; }

    public Guid Id => Model.Id;
    public AppId AppId => Model.AppId;
    public string Title => Model.Title;

    [ObservableProperty]
    private QueueJobStatus _status;

    [ObservableProperty]
    private double _percentage;

    [ObservableProperty]
    private string _speedFormatted = "0.00 B/s";

    [ObservableProperty]
    private string _etaFormatted = "--";

    [ObservableProperty]
    private string? _currentFile;

    [ObservableProperty]
    private string? _errorMessage;

    public bool CanPause => Status == QueueJobStatus.Running;
    public bool CanResume => Status == QueueJobStatus.Paused;
    public bool CanCancel => !Model.IsTerminal;

    public QueueJobItemViewModel(QueueJob model, IDownloadQueueManager queueManager)
    {
        Model = model;
        _queueManager = queueManager;
        _status = model.Status;
        _percentage = model.ProgressPercentage;
        _errorMessage = model.ErrorMessage;
    }

    public void UpdateStatus(QueueJobStatus newStatus, string? error = null)
    {
        Status = newStatus;
        if (error != null) ErrorMessage = error;
        OnPropertyChanged(nameof(CanPause));
        OnPropertyChanged(nameof(CanResume));
        OnPropertyChanged(nameof(CanCancel));
    }

    public void UpdateProgress(double percentage, string speed, string eta, string? file)
    {
        Percentage = percentage;
        SpeedFormatted = speed;
        EtaFormatted = eta;
        CurrentFile = file;
    }

    [RelayCommand]
    public void Pause()
    {
        _queueManager.PauseJob(Id);
        UpdateStatus(QueueJobStatus.Paused);
    }

    [RelayCommand]
    public void Resume()
    {
        _queueManager.ResumeJob(Id);
        UpdateStatus(QueueJobStatus.Running);
    }

    [RelayCommand]
    public void Cancel()
    {
        _queueManager.CancelJob(Id);
        UpdateStatus(QueueJobStatus.Cancelled);
    }
}
